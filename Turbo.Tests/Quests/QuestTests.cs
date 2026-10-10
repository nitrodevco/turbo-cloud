using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Achievements;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// Quests as the AS3 quest window, details and tracker use them: the list is each campaign's
/// current quest with the campaign's count, a player does one quest at a time (AcceptQuest /
/// ActivateQuest -> Quest, RejectQuest -> QuestCancelled), and the counted facts move it on to
/// QuestCompleted and its reward.
/// </summary>
public sealed class QuestTests : IDisposable
{
    private const int PLAYER = 1;
    private const int GAMER = 1;
    private const int DOTS = 2;
    private const int FIRST_BADGE = 10;
    private const int SECOND_BADGE = 11;
    private const int PET_FOOD = 20;

    private static readonly DateTime NOW = new(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public QuestTests()
    {
        _db.Insert(
            new QuestCampaignEntity
            {
                Id = GAMER,
                Code = "bawcollab_worlds",
                SortOrder = 1,
                EndsAt = NOW.AddDays(1),
            }
        );
        _db.Insert(
            new QuestCampaignEntity
            {
                Id = DOTS,
                Code = "connect_dots26",
                SortOrder = 2,
            }
        );
        _db.Insert(Quest(FIRST_BADGE, GAMER, QuestTypes.WEAR_BADGE, "W2601", 1, 0, 0));
        _db.Insert(Quest(SECOND_BADGE, GAMER, QuestTypes.WEAR_BADGE, "W2610", 1, 0, 1));
        _db.Insert(Quest(PET_FOOD, DOTS, QuestTypes.PET_EAT, "", 2, 50, 0));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task The_list_is_each_campaigns_current_quest_and_opens_the_window()
    {
        await Grain().SendQuestsAsync(true, Ct);

        var list = Single<QuestsMessageComposer>();
        list.OpenWindow.Should().BeTrue();
        list.Quests.Select(x =>
                (x.Id, x.CampaignCode, x.CompletedQuestsInCampaign, x.QuestCountInCampaign)
            )
            .Should()
            .Equal((FIRST_BADGE, "bawcollab_worlds", 0, 2), (PET_FOOD, "connect_dots26", 0, 1));
        (list.Quests[0].IsSeasonal, list.Quests[0].SecondsLeft).Should().Be((true, 86400));
        list.Quests[1].IsSeasonal.Should().BeFalse();
    }

    [Fact]
    public async Task Accepting_a_quest_drops_the_one_being_done()
    {
        await Grain().AcceptAsync(FIRST_BADGE, Ct);
        await Grain().AcceptAsync(PET_FOOD, Ct);

        Sent<QuestMessageComposer>()
            .Select(x => (x.Quest.Id, x.Quest.Accepted))
            .Should()
            .Equal((FIRST_BADGE, true), (PET_FOOD, true));
        _db.CreateDbContext()
            .PlayerQuests.OrderBy(x => x.QuestEntityId)
            .Select(x => x.Accepted)
            .Should()
            .Equal(false, true);
    }

    [Fact]
    public async Task Only_a_campaigns_current_quest_can_be_accepted()
    {
        await Grain().AcceptAsync(SECOND_BADGE, Ct);
        await Grain().AcceptAsync(999, Ct);

        Sent<QuestMessageComposer>().Should().BeEmpty();
        _db.CreateDbContext().PlayerQuests.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejecting_the_quest_cancels_it()
    {
        await Grain().AcceptAsync(FIRST_BADGE, Ct);
        await Grain().RejectAsync(FIRST_BADGE, Ct);
        await Grain().RejectAsync(0, Ct);

        var cancelled = Single<QuestCancelledMessageComposer>();
        (cancelled.Expired, cancelled.Quest.Id, cancelled.Quest.Accepted)
            .Should()
            .Be((false, FIRST_BADGE, false));
    }

    [Fact]
    public async Task Wearing_the_quests_badge_completes_it_and_the_next_one_is_current()
    {
        await Grain().AcceptAsync(FIRST_BADGE, Ct);
        await Grain().RecordAsync(QuestTypes.WEAR_BADGE, "W2610", Ct);
        await Grain().RecordAsync(QuestTypes.WEAR_BADGE, "w2601", Ct);
        await Grain().SendQuestsAsync(false, Ct);

        var completed = Single<QuestCompletedMessageComposer>();
        (completed.ShowDialog, completed.Quest.Id, completed.Quest.CompletedSteps)
            .Should()
            .Be((true, FIRST_BADGE, 1));
        completed.Quest.CompletedQuestsInCampaign.Should().Be(1);
        Sent<QuestMessageComposer>().Should().ContainSingle("the wrong badge counts nothing");
        Single<QuestsMessageComposer>()
            .Quests[0]
            .Should()
            .Match<Turbo.Primitives.Quests.Snapshots.QuestSnapshot>(x =>
                x.Id == SECOND_BADGE && x.CompletedQuestsInCampaign == 1 && !x.Accepted
            );
    }

    [Fact]
    public async Task Each_step_is_sent_and_the_last_pays_the_reward()
    {
        await Grain().AcceptAsync(PET_FOOD, Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);

        Sent<QuestMessageComposer>().Select(x => x.Quest.CompletedSteps).Should().Equal(0, 1);
        Single<QuestCompletedMessageComposer>().Quest.CompletedSteps.Should().Be(2);
        var credit = _fakes.Log.Of("CreditAsync").Should().ContainSingle().Subject;
        (credit.Args[0], credit.Args[1], credit.Args[2])
            .Should()
            .Be((CurrencyKind.ActivityPoints(0), 50, $"quest:{PET_FOOD}"));
    }

    [Fact]
    public async Task A_reward_the_wallet_refuses_leaves_the_quest_to_be_paid_on_the_next_step()
    {
        var refused = true;

        _fakes.Handlers["CreditAsync"] = _ =>
            Task.FromResult(refused ? WalletCreditResult.Rejected : WalletCreditResult.Applied);

        await Grain().AcceptAsync(PET_FOOD, Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);

        Sent<QuestCompletedMessageComposer>().Should().BeEmpty();
        _db.CreateDbContext()
            .PlayerQuests.Single()
            .Should()
            .Match<PlayerQuestEntity>(x => x.Accepted && x.CompletedAt == null);

        refused = false;
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);

        Single<QuestCompletedMessageComposer>().Quest.Id.Should().Be(PET_FOOD);
        _fakes
            .Log.Of("CreditAsync")
            .Select(x => x.Args[2])
            .Should()
            .Equal($"quest:{PET_FOOD}", $"quest:{PET_FOOD}");
    }

    [Fact]
    public async Task The_tracker_asking_after_a_quest_is_done_gets_the_campaigns_next_quest()
    {
        await Grain().AcceptAsync(FIRST_BADGE, Ct);
        await Grain().RecordAsync(QuestTypes.WEAR_BADGE, "W2601", Ct);
        var sentBefore = Sent<QuestMessageComposer>().Count();

        await Grain().OpenTrackerAsync(Ct);

        Sent<QuestMessageComposer>()
            .Skip(sentBefore)
            .Select(x => (x.Quest.Id, x.Quest.Accepted))
            .Should()
            .Equal((SECOND_BADGE, true));
        _db.CreateDbContext()
            .PlayerQuests.Single(x => x.QuestEntityId == SECOND_BADGE)
            .Accepted.Should()
            .BeTrue();
    }

    [Fact]
    public async Task The_tracker_asking_after_a_campaigns_last_quest_gets_nothing()
    {
        await Grain().AcceptAsync(PET_FOOD, Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);
        var sentBefore = Sent<QuestMessageComposer>().Count();

        await Grain().OpenTrackerAsync(Ct);

        Sent<QuestMessageComposer>().Skip(sentBefore).Should().BeEmpty();
        _db.CreateDbContext().PlayerQuests.Should().NotContain(x => x.Accepted);
    }

    [Fact]
    public async Task The_tracker_asking_while_a_quest_is_being_done_gets_that_quest()
    {
        await Grain().AcceptAsync(PET_FOOD, Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);

        await Grain().OpenTrackerAsync(Ct);

        Sent<QuestMessageComposer>()
            .Last()
            .Quest.Should()
            .Match<Turbo.Primitives.Quests.Snapshots.QuestSnapshot>(x =>
                x.Id == PET_FOOD && x.Accepted && x.CompletedSteps == 1
            );
    }

    [Fact]
    public async Task OpenQuestTracker_asks_the_players_quests_for_the_tracker()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("OpenQuestTrackerMessageEvent"),
            [],
            playerId: PLAYER
        );

        harness
            .Fakes.Log.Of(nameof(IPlayerQuestGrain.OpenTrackerAsync))
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)PLAYER);
    }

    [Fact]
    public async Task A_campaign_done_is_listed_as_done()
    {
        await Grain().AcceptAsync(PET_FOOD, Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);
        await Grain().RecordAsync(QuestTypes.PET_EAT, "", Ct);
        await Grain().SendQuestsAsync(true, Ct);

        var dots = Single<QuestsMessageComposer>().Quests[1];
        (dots.Id, dots.CompletedQuestsInCampaign, dots.QuestCountInCampaign).Should().Be((0, 1, 1));
    }

    [Fact]
    public void A_badge_put_on_and_a_pet_fed_count_for_quests()
    {
        var listener = new QuestFactListener(
            _fakes.Create<Orleans.IGrainFactory>(),
            NullLogger<QuestFactListener>.Instance
        );

        listener.OnFactRecorded(PLAYER, Fact(AchievementSources.BADGE_WORN, "W2601"));
        listener.OnFactRecorded(PLAYER, Fact(AchievementSources.NUTRITION, ""));
        listener.OnFactRecorded(PLAYER, Fact(AchievementSources.MOTTO, ""));

        _fakes
            .Log.Of("RecordAsync")
            .Select(x => ((string)x.Args[0]!, (string)x.Args[1]!))
            .Should()
            .Equal((QuestTypes.WEAR_BADGE, "W2601"), (QuestTypes.PET_EAT, ""));
    }

    private static AchievementFact Fact(string source, string value) =>
        new()
        {
            Source = source,
            OperationId = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = NOW,
            Value = value,
        };

    private IPlayerQuestGrain Grain()
    {
        var grain = GrainHarness.Create(
            typeof(AchievementModule).Assembly,
            "Turbo.Achievements.Grains.PlayerQuestGrain",
            _fakes,
            _db
        );
        RoomHarness.SetField(grain, "_time", new ManualTimeProvider(new DateTimeOffset(NOW)));
        RoomHarness.SetField(grain, "_playerId", (PlayerId)PLAYER);

        return (IPlayerQuestGrain)grain;
    }

    private IEnumerable<T> Sent<T>()
        where T : IComposer =>
        _fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(x =>
                x.Args[0] switch
                {
                    IReadOnlyList<IComposer> many => many,
                    IComposer one => [one],
                    _ => [],
                }
            )
            .OfType<T>();

    private T Single<T>()
        where T : IComposer => Sent<T>().Should().ContainSingle().Subject;

    private static QuestEntity Quest(
        int id,
        int campaign,
        string type,
        string target,
        int steps,
        int reward,
        int order
    ) =>
        new()
        {
            Id = id,
            CampaignEntityId = campaign,
            LocalizationCode = $"q{id}",
            Type = type,
            Target = target,
            TotalSteps = steps,
            RewardAmount = reward,
            SortOrder = order,
        };
}
