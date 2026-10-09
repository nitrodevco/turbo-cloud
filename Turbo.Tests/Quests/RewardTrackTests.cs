using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// A player's reward track as the AS3 RewardTrack classes read it: a task's count crossing a
/// level's count adds that level's points (RewardTrackProgress, 844), a prize is claimable when
/// not premium-locked and within the points (RewardTrackClaimResult, 3381, codes 0-8), and
/// premium is bought with its costs and gives its instant points (917, codes 0-9). The levels
/// 1 / 5 / 20 for 10 / 20 / 30 points are those of Habbo's "Visit rooms" (official capture).
/// </summary>
public sealed class RewardTrackTests : IDisposable
{
    private const string TRACK = "introduction";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private RewardTrackConfig _config = new()
    {
        Tracks =
        [
            new()
            {
                Id = TRACK,
                Premium = new()
                {
                    TaskPointsBoost = 1.5,
                    InstantPoints = 100,
                    CostCredits = 10,
                },
                Tasks =
                [
                    new()
                    {
                        Id = "visit_rooms",
                        ActionType = RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM,
                        Levels =
                        [
                            new() { RequiredCount = 1, Points = 10 },
                            new() { RequiredCount = 5, Points = 20 },
                            new() { RequiredCount = 20, Points = 30 },
                        ],
                    },
                    new()
                    {
                        Id = "change_outfit",
                        ActionType = RewardTrackActionTypes.CHANGE_FIGURE,
                        Levels =
                        [
                            new() { RequiredCount = 1, Points = 10 },
                            new()
                            {
                                RequiredCount = 2,
                                Points = 40,
                                Premium = true,
                            },
                        ],
                    },
                    new()
                    {
                        Id = "premium_wave",
                        ActionType = RewardTrackActionTypes.WAVE,
                        Premium = true,
                        Levels = [new() { RequiredCount = 1, Points = 50 }],
                    },
                ],
                Prizes =
                [
                    new()
                    {
                        Id = "duckets",
                        RequiredPoints = 10,
                        RewardTypeId = "0",
                        Amount = 5,
                    },
                    new()
                    {
                        Id = "badge",
                        RequiredPoints = 10,
                        ProductType = ProductDisplayType.Badge,
                        RewardTypeId = "RT_INTRO",
                        Premium = true,
                    },
                    new()
                    {
                        Id = "far",
                        RequiredPoints = 1000,
                        RewardTypeId = "0",
                    },
                ],
            },
        ],
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RewardTrackTests()
    {
        _fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "reward-track",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task The_tracks_are_sent_with_the_players_progress()
    {
        var grain = await GrainAsync();
        await grain.RecordActionAsync(RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM, "7", Ct);

        await (await GrainAsync()).SendTracksAsync(Ct);

        var sent = Single<RewardTracksMessageComposer>();
        sent.Disabled.Should().BeFalse();
        var track = sent.Tracks.Should().ContainSingle().Subject;
        track.Points.Should().Be(10);
        track.PremiumConfig!.CostCredits.Should().Be(10);
        track.Premium.Should().BeFalse();
        track.Tasks.Select(x => (x.Id, x.ProgressCount)).Should().Contain(("visit_rooms", 1));
        track
            .Prizes.Select(x => (x.Id, x.Available, x.Claimed))
            .Should()
            .Equal(("duckets", true, false), ("badge", false, false), ("far", false, false));
    }

    [Fact]
    public async Task Each_room_counts_once_and_each_level_reached_adds_its_points()
    {
        var grain = await GrainAsync();

        foreach (var room in new[] { "1", "1", "2", "3", "4", "5" })
            await grain.RecordActionAsync(RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM, room, Ct);

        Sent<RewardTrackProgressMessageComposer>()
            .Select(x => (x.TaskId, x.ProgressCount, x.Points))
            .Should()
            .Equal(
                ("visit_rooms", 1, 10),
                ("visit_rooms", 2, 10),
                ("visit_rooms", 3, 10),
                ("visit_rooms", 4, 10),
                ("visit_rooms", 5, 30)
            );
    }

    [Fact]
    public async Task A_task_stops_counting_at_its_last_level()
    {
        var grain = await GrainAsync();

        foreach (var room in Enumerable.Range(1, 22))
            await grain.RecordActionAsync(
                RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM,
                room.ToString(),
                Ct
            );

        var last = Sent<RewardTrackProgressMessageComposer>().ToList();
        last.Should().HaveCount(20);
        last[^1].Points.Should().Be(60);
    }

    [Fact]
    public async Task Premium_tasks_and_levels_count_only_for_a_premium_owner()
    {
        var grain = await GrainAsync();

        await grain.RecordActionAsync(RewardTrackActionTypes.WAVE, "", Ct);
        await grain.RecordActionAsync(RewardTrackActionTypes.CHANGE_FIGURE, "", Ct);
        await grain.RecordActionAsync(RewardTrackActionTypes.CHANGE_FIGURE, "", Ct);

        Sent<RewardTrackProgressMessageComposer>()
            .Select(x => (x.TaskId, x.ProgressCount, x.Points))
            .Should()
            .Equal(("change_outfit", 1, 10), ("change_outfit", 2, 10));
    }

    [Fact]
    public async Task Buying_premium_debits_its_cost_and_gives_its_points_and_passed_premium_levels()
    {
        var grain = await GrainAsync();
        await grain.RecordActionAsync(RewardTrackActionTypes.CHANGE_FIGURE, "", Ct);
        await grain.RecordActionAsync(RewardTrackActionTypes.CHANGE_FIGURE, "", Ct);

        await grain.PurchasePremiumAsync(TRACK, Ct);
        await grain.PurchasePremiumAsync(TRACK, Ct);

        var debit = _fakes.Log.Of("TryDebitAsync").Should().ContainSingle().Subject;
        ((List<WalletDebitRequest>)debit.Args[0]!)
            .Should()
            .Equal(new WalletDebitRequest { CurrencyKind = CurrencyKind.Credits, Amount = 10 });
        // 10 earned, 100 instant, the passed premium level's 40 boosted by 1.5.
        Sent<RewardTrackPremiumPurchaseResultMessageComposer>()
            .Select(x => (x.Result, x.Points))
            .Should()
            .Equal(
                (RewardTrackPremiumResult.Success, 170),
                (RewardTrackPremiumResult.AlreadyOwned, 170)
            );
    }

    [Fact]
    public async Task A_premium_owner_gets_task_points_boosted_and_premium_tasks()
    {
        var grain = await GrainAsync();
        await grain.PurchasePremiumAsync(TRACK, Ct);

        await grain.RecordActionAsync(RewardTrackActionTypes.WAVE, "", Ct);

        Single<RewardTrackProgressMessageComposer>().Points.Should().Be(100 + 75);
    }

    [Fact]
    public async Task Too_few_credits_for_premium_is_code_7_and_changes_nothing()
    {
        _fakes.Handlers["TryDebitAsync"] = _ =>
            Task.FromResult(
                WalletDebitResult.InsufficientBalance(
                    new WalletDebitFailure { CurrencyKind = CurrencyKind.Credits, Amount = 10 }
                )
            );
        var grain = await GrainAsync();

        await grain.PurchasePremiumAsync(TRACK, Ct);
        await grain.SendTracksAsync(Ct);

        Single<RewardTrackPremiumPurchaseResultMessageComposer>()
            .Result.Should()
            .Be(RewardTrackPremiumResult.NotEnoughCredits);
        Single<RewardTracksMessageComposer>().Tracks[0].Premium.Should().BeFalse();
    }

    [Fact]
    public async Task Too_few_diamonds_for_premium_is_code_8()
    {
        _fakes.Handlers["TryDebitAsync"] = _ =>
            Task.FromResult(
                WalletDebitResult.InsufficientBalance(
                    new WalletDebitFailure
                    {
                        CurrencyKind = CurrencyKind.ActivityPoints((int)ActivityPointType.Diamonds),
                        Amount = 10,
                    }
                )
            );
        var grain = await GrainAsync();

        await grain.PurchasePremiumAsync(TRACK, Ct);

        Single<RewardTrackPremiumPurchaseResultMessageComposer>()
            .Result.Should()
            .Be(RewardTrackPremiumResult.NotEnoughDiamonds);
    }

    [Fact]
    public async Task A_track_without_premium_answers_code_4_and_an_unknown_one_code_2()
    {
        _config.Tracks[0].Premium = null;
        var grain = await GrainAsync();

        await grain.PurchasePremiumAsync(TRACK, Ct);
        await grain.PurchasePremiumAsync("nope", Ct);

        Sent<RewardTrackPremiumPurchaseResultMessageComposer>()
            .Select(x => x.Result)
            .Should()
            .Equal(RewardTrackPremiumResult.NotConfigured, RewardTrackPremiumResult.TrackNotFound);
    }

    [Fact]
    public async Task Claims_are_checked_in_the_clients_order_and_pay_out_once()
    {
        var grain = await GrainAsync();

        await grain.ClaimPrizeAsync(TRACK, "duckets", Ct);
        await grain.RecordActionAsync(RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM, "7", Ct);
        await grain.ClaimPrizeAsync(TRACK, "duckets", Ct);
        await grain.ClaimPrizeAsync(TRACK, "duckets", Ct);
        await grain.ClaimPrizeAsync(TRACK, "badge", Ct);
        await grain.ClaimPrizeAsync(TRACK, "nope", Ct);
        await grain.ClaimPrizeAsync("nope", "duckets", Ct);

        Sent<RewardTrackClaimResultMessageComposer>()
            .Select(x => x.Result)
            .Should()
            .Equal(
                RewardTrackClaimResult.NotEnoughPoints,
                RewardTrackClaimResult.Success,
                RewardTrackClaimResult.AlreadyClaimed,
                RewardTrackClaimResult.PremiumRequired,
                RewardTrackClaimResult.PrizeNotFound,
                RewardTrackClaimResult.TrackNotFound
            );
        var credit = _fakes.Log.Of("CreditAsync").Should().ContainSingle().Subject;
        credit.Args[0].Should().Be(CurrencyKind.ActivityPoints(0));
        credit.Args[1].Should().Be(5);
        credit.Args[2].Should().Be("rewardtrack:introduction:duckets");
    }

    [Fact]
    public async Task A_premium_prize_is_given_once_premium_is_owned()
    {
        var grain = await GrainAsync();
        await grain.PurchasePremiumAsync(TRACK, Ct);

        await grain.ClaimPrizeAsync(TRACK, "badge", Ct);

        Single<RewardTrackClaimResultMessageComposer>()
            .Result.Should()
            .Be(RewardTrackClaimResult.Success);
        _fakes.Log.Of("GiveBadgeAsync").Single().Args[0].Should().Be("RT_INTRO");
    }

    [Fact]
    public async Task Disabled_tracks_are_sent_as_disabled_and_refuse_claims_with_code_1()
    {
        _config.Enabled = false;
        var grain = await GrainAsync();

        await grain.SendTracksAsync(Ct);
        await grain.ClaimPrizeAsync(TRACK, "duckets", Ct);

        Single<RewardTracksMessageComposer>()
            .Should()
            .Match<RewardTracksMessageComposer>(x => x.Disabled && x.Tracks.IsEmpty);
        Single<RewardTrackClaimResultMessageComposer>()
            .Result.Should()
            .Be(RewardTrackClaimResult.Disabled);
    }

    [Fact]
    public void The_hotels_facts_count_the_tasks_configured_for_them()
    {
        var listener = new RewardTrackFactListener(
            Options.Create(_config),
            _fakes.Create<Orleans.IGrainFactory>(),
            NullLogger<RewardTrackFactListener>.Instance
        );

        listener.OnFactRecorded(1, Fact(AchievementSources.VISIT, "7"));
        listener.OnFactRecorded(1, Fact(AchievementSources.FIGURE, ""));
        listener.OnFactRecorded(1, Fact(AchievementSources.RESPECT_GIVEN, ""));

        _fakes
            .Log.Of("RecordActionAsync")
            .Select(x => (x.Args[0], x.Args[1]))
            .Should()
            .Equal(
                ((object?)RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM, (object?)"7"),
                ((object?)RewardTrackActionTypes.CHANGE_FIGURE, (object?)"")
            );
    }

    [Fact]
    public void A_recorded_fact_reaches_the_listeners_even_when_no_achievement_counts_it()
    {
        var heard = new List<string>();
        _fakes.Handlers["get_Current"] = _ =>
            System.Collections.Immutable.ImmutableArray<AchievementDefinition>.Empty;
        var recorder = new AchievementFactRecorder(
            _fakes.Create<IAchievementCatalog>(),
            [new HeardListener(heard)]
        );

        using var db = _db.CreateDbContext();
        recorder.Record(db, 1, Fact(AchievementSources.FIGURE, ""));

        heard.Should().Equal(AchievementSources.FIGURE);
    }

    private async Task<IPlayerRewardTrackGrain> GrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(AchievementModule).Assembly,
            "Turbo.Achievements.Grains.PlayerRewardTrackGrain",
            _fakes,
            _db
        );
        RoomHarness.SetField(grain, "_config", _config);
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);

        return (IPlayerRewardTrackGrain)grain;
    }

    private static AchievementFact Fact(string source, string value) =>
        new()
        {
            OperationId = Guid.NewGuid().ToString("N"),
            Source = source,
            Value = value,
            OccurredAtUtc = DateTime.UtcNow,
        };

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

    private sealed class HeardListener(List<string> heard) : IAchievementFactListener
    {
        public void OnFactRecorded(
            Turbo.Primitives.Players.PlayerId playerId,
            AchievementFact fact
        ) => heard.Add(fact.Source);
    }
}
