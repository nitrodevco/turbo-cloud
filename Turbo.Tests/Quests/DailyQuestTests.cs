using FluentAssertions;
using Turbo.Achievements;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Quests.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// The reception's daily quest (<c>DailyQuestWidget</c>, <c>DailyQuestElementHandler</c>): it asks
/// with <c>GetDailyQuest(easy, index)</c> for the first easy quest, the next one on "another
/// quest" and the other difficulty on its link, and reads <c>QuestDaily</c> - a quest and how many
/// of each difficulty there are, or none when every quest is done.
/// </summary>
public sealed class DailyQuestTests : IDisposable
{
    private const int PLAYER = 1;
    private const int EASY_ONE = 10;
    private const int EASY_TWO = 20;
    private const int HARD = 30;
    private const int EASY_AFTER_HARD = 31;

    private static readonly DateTime NOW = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DailyQuestTests()
    {
        // Three campaigns: their current quests are two easy ones and a hard one.
        foreach (var (id, code) in new[] { (1, "one"), (2, "two"), (3, "three") })
            _db.Insert(
                new QuestCampaignEntity
                {
                    Id = id,
                    Code = code,
                    SortOrder = id,
                }
            );

        _db.Insert(Quest(EASY_ONE, 1, easy: true, order: 0));
        _db.Insert(Quest(EASY_TWO, 2, easy: true, order: 0));
        _db.Insert(Quest(HARD, 3, easy: false, order: 0));
        _db.Insert(Quest(EASY_AFTER_HARD, 3, easy: true, order: 1));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Each_index_is_the_next_quest_of_the_difficulty_counting_round()
    {
        foreach (var index in new[] { 0, 1, 2 })
            await Grain().SendDailyQuestAsync(true, index, Ct);
        await Grain().SendDailyQuestAsync(false, 0, Ct);

        Sent()
            .Select(x => (x.Quest!.Id, x.Quest.Easy, x.EasyQuestCount, x.HardQuestCount))
            .Should()
            .Equal(
                (EASY_ONE, true, 2, 1),
                (EASY_TWO, true, 2, 1),
                (EASY_ONE, true, 2, 1),
                (HARD, false, 2, 1)
            );
    }

    [Fact]
    public async Task The_quest_being_done_is_the_one_shown()
    {
        await Grain().AcceptAsync(HARD, Ct);

        await Grain().SendDailyQuestAsync(true, 0, Ct);

        Sent().Single().Quest.Should().Match<QuestSnapshot>(x => x.Id == HARD && x.Accepted);
    }

    [Fact]
    public async Task A_difficulty_with_no_quest_left_gives_one_of_the_other()
    {
        Complete(HARD);

        await Grain().SendDailyQuestAsync(false, 0, Ct);

        var daily = Sent().Single();
        (daily.Quest!.Id, daily.EasyQuestCount, daily.HardQuestCount).Should().Be((EASY_ONE, 3, 0));
    }

    [Fact]
    public async Task Every_quest_done_sends_none()
    {
        foreach (var id in new[] { EASY_ONE, EASY_TWO, HARD, EASY_AFTER_HARD })
            Complete(id);

        await Grain().SendDailyQuestAsync(true, 0, Ct);

        Sent().Single().Quest.Should().BeNull();
    }

    [Fact]
    public async Task GetDailyQuest_asks_the_players_quests_for_the_difficulty_and_index()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("GetDailyQuestMessageEvent"),
            PacketHarness.Payload(w => w.Bool(false).Int(3)),
            playerId: PLAYER
        );

        var call = harness
            .Fakes.Log.Of(nameof(IPlayerQuestGrain.SendDailyQuestAsync))
            .Should()
            .ContainSingle()
            .Subject;
        (call.Key, call.Args[0], call.Args[1]).Should().Be(((long)PLAYER, false, 3));
    }

    [Fact]
    public void QuestDaily_is_a_flag_then_the_quest_and_both_counts()
    {
        var none = PacketHarness.Encode(
            new QuestDailyMessageComposer
            {
                Quest = null,
                EasyQuestCount = 0,
                HardQuestCount = 0,
            }
        );
        none.PopBoolean().Should().BeFalse();
        none.End.Should().BeTrue();

        var quest = new QuestSnapshot
        {
            CampaignCode = "one",
            CompletedQuestsInCampaign = 0,
            QuestCountInCampaign = 1,
            ActivityPointType = 0,
            Id = EASY_ONE,
            Accepted = false,
            Type = QuestTypes.PET_EAT,
            ImageVersion = "",
            RewardCurrencyAmount = 5,
            LocalizationCode = "q10",
            CompletedSteps = 0,
            TotalSteps = 1,
            SortOrder = 1,
            CatalogPageName = "",
            ChainCode = "",
            Easy = true,
            IsSeasonal = false,
            SecondsLeft = 0,
        };
        // The quest is QuestMessageData, as the Quest message writes it.
        var alone = PacketHarness.Encode(new QuestMessageComposer { Quest = quest });
        var one = PacketHarness.Encode(
            new QuestDailyMessageComposer
            {
                Quest = quest,
                EasyQuestCount = 2,
                HardQuestCount = 1,
            }
        );

        one.PopBoolean().Should().BeTrue();
        one.PopBytes(alone.Remaining).Should().Equal(alone.PopBytes(alone.Remaining));
        (one.PopInt(), one.PopInt()).Should().Be((2, 1));
        one.End.Should().BeTrue();
    }

    private void Complete(int questId) =>
        _db.Insert(
            new PlayerQuestEntity
            {
                Id = questId,
                PlayerEntityId = PLAYER,
                QuestEntityId = questId,
                Progress = 1,
                CompletedAt = NOW.AddDays(-1),
            }
        );

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

    private IEnumerable<QuestDailyMessageComposer> Sent() =>
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
            .OfType<QuestDailyMessageComposer>();

    private static QuestEntity Quest(int id, int campaign, bool easy, int order) =>
        new()
        {
            Id = id,
            CampaignEntityId = campaign,
            LocalizationCode = $"q{id}",
            Type = QuestTypes.PET_EAT,
            Easy = easy,
            SortOrder = order,
        };
}
