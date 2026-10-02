using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementProgressionTests : IDisposable
{
    private readonly SqliteDb _database = new();
    private readonly Fakes _fakes = new();
    private readonly TestCatalog _catalog = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly AchievementObserverRegistry _observers = new(
        NullLogger<AchievementObserverRegistry>.Instance
    );
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AchievementProgressionTests() =>
        _database.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "progression-test",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            }
        );

    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }

    private IPlayerAchievementGrain NewGrain()
    {
        var grain = GrainHarness.Create(
            typeof(AchievementModule).Assembly,
            "Turbo.Achievements.Grains.PlayerAchievementGrain",
            _fakes,
            _database
        );
        var recorder = new AchievementFactRecorder(_catalog);
        RoomHarness.SetField(grain, "_catalog", _catalog);
        RoomHarness.SetField(grain, "_recorder", recorder);
        RoomHarness.SetField(grain, "_rewards", new AchievementRewardRegistry());
        RoomHarness.SetField(grain, "_observers", _observers);
        RoomHarness.SetField(grain, "_time", _clock);
        RoomHarness.SetField(
            grain,
            "_evaluator",
            new AchievementStateEvaluator(_database, recorder)
        );
        return (IPlayerAchievementGrain)grain;
    }

    /// <summary>What an import persists: every published revision stays readable for frozen awards.</summary>
    private void StoreRevisions(IEnumerable<AchievementDefinition> definitions)
    {
        foreach (var definition in definitions)
            _database.Insert(
                new AchievementDefinitionEntity
                {
                    AchievementId = definition.Id,
                    Revision = definition.Revision,
                    DefinitionJson = JsonSerializer.Serialize(definition),
                }
            );
    }

    private async Task RecordAsync(string operation, string source, long amount, string value = "")
    {
        await using var db = await _database.CreateDbContextAsync(Ct);
        new AchievementFactRecorder(_catalog).Record(
            db,
            1,
            new()
            {
                OperationId = operation,
                Source = source,
                Amount = amount,
                Value = value,
                OccurredAtUtc = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync(Ct);
    }

    private static AchievementDefinition Definition(
        int id,
        string source = AchievementSources.FIGURE
    ) =>
        new()
        {
            Id = id,
            Revision = 1,
            Key = "test-" + id,
            Category = "identity",
            Source = source,
            Reducer = AchievementReducer.Counter,
            Levels =
            [
                new() { Requirement = 1, BadgeCode = "ACH_Test1" },
                new() { Requirement = 2, BadgeCode = "ACH_Test2" },
                new() { Requirement = 3, BadgeCode = "ACH_Test3" },
            ],
        };

    [Fact]
    public async Task OfflineCrossedLevelsAreOrderedAndReactivationDoesNotRepeatRewards()
    {
        _catalog.Current = [Definition(100000)];
        await RecordAsync("action-1", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await NewGrain().ProcessAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        // A completed level stays open until the player has been told, and nobody was online.
        var awards = progress.ReadOpenAwards();
        Assert.Equal([1, 2, 3], awards.Select(x => x.Level));
        Assert.All(awards, award => Assert.True(award.Completed));
        Assert.Equal(3, progress.CompletedLevel);
        Assert.Equal(30, progress.ScoreEarned);
        Assert.False(progress.PendingDelivery);
        Assert.Equal(
            3,
            _fakes.Log.Calls.Count(x => x.Method == nameof(IPlayerBadgeGrain.GrantAchievementAsync))
        );
        var projection = await db.AchievementProjections.SingleAsync(Ct);
        Assert.Equal(30, projection.Score);
        Assert.Equal(3, projection.EarnedLevels);
        Assert.True((await db.AchievementFacts.SingleAsync(Ct)).Processed);
    }

    [Fact]
    public async Task ObserversHearEachCompletedLevelOnceAndNeverAgainOnReplay()
    {
        var observer = new RecordingObserver();
        _observers.Register([observer]);
        _catalog.Current = [Definition(100000)];
        await RecordAsync("observed-1", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await observer.WaitForAsync(3, Ct);
        await NewGrain().ProcessAsync(Ct);
        await NewGrain().RetryAsync(Ct);
        var heard = observer.Heard.OrderBy(x => x.Level).ToList();
        Assert.Equal([1, 2, 3], heard.Select(x => x.Level));
        Assert.Equal(["ACH_Test1", "ACH_Test2", "ACH_Test3"], heard.Select(x => x.BadgeCode));
        Assert.All(
            heard,
            x =>
            {
                Assert.Equal(1, x.PlayerId.Value);
                Assert.Equal(100000, x.AchievementId);
                Assert.Equal("test-100000", x.Key);
                Assert.Equal("identity", x.Category);
                Assert.Equal(1, x.Revision);
                Assert.Equal(10, x.Score);
            }
        );
    }

    [Fact]
    public async Task ThrowingObserverNeitherBlocksDeliveryNorOtherObservers()
    {
        var healthy = new RecordingObserver();
        _observers.Register([new ThrowingObserver(), healthy]);
        _catalog.Current = [Definition(100000)];
        await RecordAsync("observed-2", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await healthy.WaitForAsync(3, Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.Equal(3, (await db.AchievementProgress.SingleAsync(Ct)).CompletedLevel);
        Assert.Equal(30, (await db.AchievementProjections.SingleAsync(Ct)).Score);
    }

    [Fact]
    public async Task ObserverIsNotToldOfALevelWhoseAwardIsBlocked()
    {
        var observer = new RecordingObserver();
        _observers.Register([observer]);
        _fakes.Handlers[nameof(IPlayerBadgeGrain.GrantAchievementAsync)] = call =>
            (int)call.Args[1]! == 2
                ? Task.FromException(new IOException("badge failure"))
                : Task.CompletedTask;
        _catalog.Current = [Definition(100000)];
        await RecordAsync("observed-3", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await observer.WaitForAsync(1, Ct);
        await Task.Delay(100, Ct);
        Assert.Equal([1], observer.Heard.Select(x => x.Level));
    }

    [Fact]
    public async Task DisposedObserverRegistrationStopsNotifications()
    {
        var observer = new RecordingObserver();
        _observers.Register([observer]).Dispose();
        _catalog.Current = [Definition(100000)];
        await RecordAsync("observed-4", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await Task.Delay(100, Ct);
        Assert.Empty(observer.Heard);
    }

    [Fact]
    public void RepeatedObserverRejectsTheWholeBatch()
    {
        var first = new RecordingObserver();
        _observers.Register([first]);
        Assert.Throws<ArgumentException>(() =>
            _observers.Register([new RecordingObserver(), first])
        );
        Assert.Throws<ArgumentException>(() => _observers.Register([]));
        var other = new RecordingObserver();
        _observers.Register([other]);
    }

    [Fact]
    public async Task BlockedAchievementDoesNotPreventAnotherAchievementAndRetryPreservesFrozenAwards()
    {
        _catalog.Current = [Definition(100000), Definition(100001)];
        StoreRevisions(_catalog.Current);
        _fakes.Handlers[nameof(IPlayerBadgeGrain.GrantAchievementAsync)] = call =>
            (int)call.Args[0]! == 100000
                ? Task.FromException(new IOException("temporary badge failure"))
                : Task.CompletedTask;
        await RecordAsync("action-2", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await using (var db = await _database.CreateDbContextAsync(Ct))
        {
            Assert.Equal(3, await db.AchievementProgress.SumAsync(x => x.CompletedLevel, Ct));
            Assert.Equal(30, (await db.AchievementProjections.SingleAsync(Ct)).Score);
            var blocked = await db.AchievementProgress.SingleAsync(
                x => x.AchievementId == 100000,
                Ct
            );
            Assert.True(blocked.PendingDelivery);
            Assert.NotNull(blocked.ReadOpenAwards().Single(x => x.Level == 1).BlockedReason);
        }
        _catalog.Current = _catalog
            .Current.Select(x =>
                x with
                {
                    Revision = 2,
                    Levels = x.Levels.Select(l => l with { Score = 999 }).ToImmutableArray(),
                }
            )
            .ToImmutableArray();
        _fakes.Handlers.TryRemove(nameof(IPlayerBadgeGrain.GrantAchievementAsync), out _);
        await NewGrain().RetryAsync(Ct);
        await using (var db = await _database.CreateDbContextAsync(Ct))
        {
            Assert.Equal(6, await db.AchievementProgress.SumAsync(x => x.CompletedLevel, Ct));
            Assert.Equal(60, (await db.AchievementProjections.SingleAsync(Ct)).Score);
            Assert.Equal(6, (await db.AchievementProjections.SingleAsync(Ct)).EarnedLevels);
        }
    }

    [Fact]
    public async Task AnnouncedLevelsLeaveTheOpenListAndEachIsAnnouncedOnce()
    {
        _fakes.Handlers["TrySendComposerAsync"] = _ => Task.FromResult(true);
        _catalog.Current = [Definition(100000)];
        await RecordAsync("announce-1", AchievementSources.FIGURE, 3);

        await NewGrain().ProcessAsync(Ct);
        await NewGrain().ProcessAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        Assert.Equal(AchievementOpenAwards.NONE, progress.OpenAwards);
        Assert.Equal(3, progress.CompletedLevel);
        Assert.Equal(30, progress.ScoreEarned);
        Assert.Equal(30, (await db.AchievementProjections.SingleAsync(Ct)).Score);
        Assert.Equal(3, _fakes.Log.Calls.Count(x => x.Method == "TrySendComposerAsync"));
    }

    [Fact]
    public async Task BlockedAwardsAreListedForOperatorsAndRetryClearsThem()
    {
        _catalog.Current = [Definition(100000)];
        _fakes.Handlers[nameof(IPlayerBadgeGrain.GrantAchievementAsync)] = _ =>
            Task.FromException(new IOException("badge failure"));
        await RecordAsync("blocked-1", AchievementSources.FIGURE, 2);
        await NewGrain().ProcessAsync(Ct);

        var pending = await NewGrain().GetPendingAwardsAsync(Ct);

        // Both undelivered levels are listed; only the one delivery stopped at has a reason.
        Assert.Equal(
            ["achievement:1:100000:1", "achievement:1:100000:2"],
            pending.Select(x => x.AwardKey)
        );
        Assert.NotNull(pending[0].BlockedReason);
        Assert.Null(pending[1].BlockedReason);
        _fakes.Handlers.TryRemove(nameof(IPlayerBadgeGrain.GrantAchievementAsync), out _);
        await NewGrain().RetryAsync(Ct);
        Assert.Empty(await NewGrain().GetPendingAwardsAsync(Ct));
        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        Assert.False(progress.PendingDelivery);
        Assert.Equal(2, progress.CompletedLevel);
    }

    [Fact]
    public async Task LevelsEarnedUnderDifferentRevisionsEachDeliverTheirOwnFrozenScore()
    {
        var first = Definition(100000);
        _catalog.Current = [first];
        StoreRevisions([first]);
        _fakes.Handlers[nameof(IPlayerBadgeGrain.GrantAchievementAsync)] = _ =>
            Task.FromException(new IOException("badge failure"));
        await RecordAsync("revision-1", AchievementSources.FIGURE, 2);
        await NewGrain().ProcessAsync(Ct);
        _catalog.Current =
        [
            first with
            {
                Revision = 2,
                Levels = first.Levels.Select(x => x with { Score = 999 }).ToImmutableArray(),
            },
        ];
        _fakes.Handlers.TryRemove(nameof(IPlayerBadgeGrain.GrantAchievementAsync), out _);
        await RecordAsync("revision-2", AchievementSources.FIGURE, 1);

        await NewGrain().ProcessAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        Assert.Equal(3, progress.CompletedLevel);
        Assert.Equal(10 + 10 + 999, progress.ScoreEarned);
        Assert.Equal(
            [1, 1, 2],
            progress.ReadOpenAwards().OrderBy(x => x.Level).Select(x => x.Revision)
        );
        Assert.Equal(1019, (await db.AchievementProjections.SingleAsync(Ct)).Score);
    }

    [Fact]
    public async Task ListShowsEnabledAndOffSeasonAndHidesDisabledAndUntouchedArchived()
    {
        _catalog.Current =
        [
            Definition(100000),
            Definition(100001) with
            {
                State = AchievementState.OffSeason,
            },
            Definition(100002) with
            {
                State = AchievementState.Disabled,
            },
            Definition(100003) with
            {
                State = AchievementState.Archived,
            },
        ];

        var listed = await NewGrain().GetAchievementsAsync(Ct);

        Assert.Equal(
            [(100000, AchievementState.Enabled), (100001, AchievementState.OffSeason)],
            listed.Select(x => (x.AchievementId, x.State))
        );
    }

    [Fact]
    public async Task AnArchivedAchievementIsListedOnceThePlayerHasProgressOnIt()
    {
        _catalog.Current = [Definition(100003) with { State = AchievementState.Archived }];
        _database.Insert(
            new AchievementProgressEntity
            {
                PlayerId = 1,
                AchievementId = 100003,
                Value = 1,
                EarnedLevel = 1,
            }
        );

        var listed = await NewGrain().GetAchievementsAsync(Ct);

        var archived = Assert.Single(listed);
        Assert.Equal(AchievementState.Archived, archived.State);
    }

    [Fact]
    public async Task ArchiveShowsAllListsEveryArchivedAchievementToEveryone()
    {
        _catalog.Current = [Definition(100003) with { State = AchievementState.Archived }];
        var grain = NewGrain();
        RoomHarness.SetField(grain, "_config", new AchievementConfig { ArchiveShowsAll = true });

        var listed = await grain.GetAchievementsAsync(Ct);

        Assert.Equal(AchievementState.Archived, Assert.Single(listed).State);
    }

    [Fact]
    public async Task OnlyEnabledAchievementsAreBoundToNewFacts()
    {
        _catalog.Current =
        [
            Definition(100000),
            Definition(100001) with
            {
                State = AchievementState.OffSeason,
            },
            Definition(100002) with
            {
                State = AchievementState.Disabled,
            },
            Definition(100003) with
            {
                State = AchievementState.Archived,
            },
        ];

        await RecordAsync("bind-state", AchievementSources.FIGURE, 1);

        await using var db = await _database.CreateDbContextAsync(Ct);
        var bindings = (await db.AchievementFacts.SingleAsync(Ct)).BindingsJson;
        Assert.Contains("100000", bindings);
        Assert.DoesNotContain("100001", bindings);
        Assert.DoesNotContain("100002", bindings);
        Assert.DoesNotContain("100003", bindings);
    }

    [Fact]
    public async Task ReconcileOpensNoAwardsForAnArchivedAchievementWithQualifyingProgress()
    {
        _catalog.Current = [Definition(100003) with { State = AchievementState.Archived }];
        _database.Insert(
            new AchievementProgressEntity
            {
                PlayerId = 1,
                AchievementId = 100003,
                Value = 3,
            }
        );

        await NewGrain().ReconcileAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        Assert.Equal(0, progress.EarnedLevel);
        Assert.False(progress.PendingDelivery);
    }

    [Fact]
    public async Task ChangingOnlyTheStateMakesReconcileEvaluateThePlayerAgain()
    {
        _catalog.Current = [Definition(100000)];
        var grain = NewGrain();
        await grain.ReconcileAsync(Ct);
        Assert.Equal(1, NormalizeCalls());

        _catalog.Current = [Definition(100000) with { State = AchievementState.Archived }];
        await grain.ReconcileAsync(Ct);

        Assert.Equal(2, NormalizeCalls());
    }

    [Fact]
    public async Task AccountAgeUsesCreationImmediatelyWithoutLaunchConfiguration()
    {
        _catalog.Current =
        [
            Definition(100000, AchievementSources.ACCOUNT_AGE) with
            {
                Reducer = AchievementReducer.Maximum,
            },
        ];
        await NewGrain().ReconcileAsync(Ct);
        var achievements = await NewGrain().GetAchievementsAsync(Ct);
        Assert.True(achievements.Single().FinalLevel);
        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.True((await db.AchievementProgress.SingleAsync(Ct)).Value >= 10);
        Assert.Equal(30, (await db.AchievementProjections.SingleAsync(Ct)).Score);
    }

    [Fact]
    public async Task ZeroMembershipLevelRequiresAnEligibleIntervalEvenWithLegacyZeroProgress()
    {
        var membership = AchievementDefaults.Definitions.Single(x => x.Key == "hc-duration");
        _catalog.Current = [membership];
        _database.Insert(
            new AchievementProgressEntity
            {
                PlayerId = 1,
                AchievementId = membership.Id,
                Value = 0,
            }
        );
        await NewGrain().ReconcileAsync(Ct);
        await using (var check = await _database.CreateDbContextAsync(Ct))
            Assert.Equal(0, (await check.AchievementProgress.SingleAsync(Ct)).EarnedLevel);

        _database.Insert(
            new PlayerSubscriptionEntity
            {
                PlayerEntityId = 1,
                SubscriptionType = SubscriptionType.HabboClub,
                FirstSubscribedAt = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1).AddDays(31),
                TotalDaysSubscribed = 31,
            }
        );
        await NewGrain().ReconcileAsync(Ct);
        await NewGrain().ReconcileAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var award = Assert.Single((await db.AchievementProgress.SingleAsync(Ct)).ReadOpenAwards());
        Assert.Equal(1, award.Level);
        Assert.Equal(2, award.Revision);
        Assert.True(award.Completed);
        Assert.Equal(10, (await db.AchievementProjections.SingleAsync(Ct)).Score);
    }

    [Fact]
    public async Task RevisedMembershipDisplayUnitsPreserveFrozenAwardsAndRawProgress()
    {
        var legacy = AchievementDefaults.Definitions.Single(x => x.Key == "hc-duration") with
        {
            Revision = 1,
            UnitDivisor = 86400,
            Levels =
            [
                new()
                {
                    Requirement = 1,
                    BadgeCode = "ACH_BasicClub1",
                    Score = 10,
                },
            ],
        };
        _catalog.Current = [legacy];
        StoreRevisions([legacy]);
        _database.Insert(
            new PlayerSubscriptionEntity
            {
                PlayerEntityId = 1,
                SubscriptionType = SubscriptionType.HabboClub,
                FirstSubscribedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(31),
                TotalDaysSubscribed = 33,
            }
        );
        await NewGrain().ReconcileAsync(Ct);
        _catalog.Current = [AchievementDefaults.Definitions.Single(x => x.Key == "hc-duration")];
        await NewGrain().ReconcileAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        var retained = Assert.Single(progress.ReadOpenAwards());
        Assert.Equal(1, retained.Revision);
        Assert.Equal(1, progress.CompletedLevel);
        Assert.Equal(10, progress.ScoreEarned);
        Assert.True(progress.Value >= 2 * 86400);
        Assert.Equal(10, (await db.AchievementProjections.SingleAsync(Ct)).Score);
    }

    [Fact]
    public async Task AdmittedFactsBindDefinitionsByRevisionKeyOnly()
    {
        _catalog.Current = [Definition(100000)];
        await RecordAsync("action-slim", AchievementSources.FIGURE, 1);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var bindings = (await db.AchievementFacts.SingleAsync(Ct)).BindingsJson;
        Assert.DoesNotContain("Levels", bindings);
        Assert.Contains("\"Revision\":1", bindings);
    }

    [Fact]
    public async Task FactStaysProcessableWhenItsRevisionIsOnlyInStorageAfterTheCatalogMovesOn()
    {
        var first = Definition(100000);
        _database.Insert(
            new AchievementDefinitionEntity
            {
                AchievementId = first.Id,
                Revision = first.Revision,
                DefinitionJson = JsonSerializer.Serialize(first),
            }
        );
        _catalog.Current = [first];
        await RecordAsync("action-old-revision", AchievementSources.FIGURE, 2);
        _catalog.Current = [first with { Revision = 2 }];
        await NewGrain().ProcessAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var awards = (await db.AchievementProgress.SingleAsync(Ct)).ReadOpenAwards();
        Assert.Equal([1, 2], awards.Select(x => x.Level));
        Assert.All(awards, x => Assert.Equal(1, x.Revision));
        Assert.True((await db.AchievementFacts.SingleAsync(Ct)).Processed);
    }

    [Fact]
    public async Task FactWithAMissingRevisionStaysUnprocessed()
    {
        _catalog.Current = [Definition(100000)];
        await RecordAsync("action-missing-revision", AchievementSources.FIGURE, 1);
        _catalog.Current = [Definition(100000) with { Revision = 2 }];
        await Assert.ThrowsAsync<InvalidOperationException>(() => NewGrain().ProcessAsync(Ct));
        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.False((await db.AchievementFacts.SingleAsync(Ct)).Processed);
        Assert.Empty(await db.AchievementProgress.ToListAsync(Ct));
    }

    [Fact]
    public async Task DistinctValuesLiveInTheirOwnTableAndRepeatsAreNotRecounted()
    {
        _catalog.Current = [Definition(100000) with { Reducer = AchievementReducer.Distinct }];
        await RecordAsync("room-1", AchievementSources.FIGURE, 0, "room-a");
        await RecordAsync("room-2", AchievementSources.FIGURE, 0, "room-a");
        await RecordAsync("room-3", AchievementSources.FIGURE, 0, "room-b");
        await NewGrain().ProcessAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        Assert.Equal(2, progress.Value);
        Assert.Equal(2, progress.DistinctCount);
        Assert.Equal(
            ["room-a", "room-b"],
            await db
                .AchievementDistinctValues.OrderBy(x => x.Value)
                .Select(x => x.Value)
                .ToListAsync(Ct)
        );
        Assert.Equal(2, progress.EarnedLevel);
    }

    [Fact]
    public async Task ListingWritesNothing()
    {
        _catalog.Current = [Definition(100000)];
        await RecordAsync("action-list", AchievementSources.FIGURE, 3);

        var listed = await NewGrain().GetAchievementsAsync(Ct);

        Assert.Single(listed);
        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.False((await db.AchievementFacts.SingleAsync(Ct)).Processed);
        Assert.Empty(await db.AchievementProgress.ToListAsync(Ct));
        Assert.Empty(await db.AchievementProjections.ToListAsync(Ct));
    }

    [Fact]
    public async Task ReconcileDoesCatalogWorkOnlyWhenTheCatalogRevisionsChange()
    {
        _catalog.Current = [StateDefinition(100000)];
        StoreRevisions(_catalog.Current);
        var grain = NewGrain();
        await grain.ReconcileAsync(Ct);
        await using (var db = await _database.CreateDbContextAsync(Ct))
            Assert.NotEqual("", (await db.AchievementProjections.SingleAsync(Ct)).ReconciledStamp);
        var stateFacts = await StateFactCountAsync();

        await grain.ReconcileAsync(Ct);

        Assert.Equal(1, NormalizeCalls());
        Assert.Equal(stateFacts, await StateFactCountAsync());
        _catalog.Current = [StateDefinition(100000) with { Revision = 2 }];
        await grain.ReconcileAsync(Ct);
        Assert.Equal(2, NormalizeCalls());
        Assert.True(await StateFactCountAsync() > stateFacts);
    }

    [Fact]
    public async Task IncrementalTotalsMatchTheRecomputeAndAnOperatorReconcileRepairsDrift()
    {
        _catalog.Current = [Definition(100000), Definition(100001)];
        await RecordAsync("action-totals", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await using (var db = await _database.CreateDbContextAsync(Ct))
        {
            var projection = await db.AchievementProjections.SingleAsync(Ct);
            Assert.Equal(60, projection.Score);
            Assert.Equal(6, projection.EarnedLevels);
            projection.Score = 999;
            projection.EarnedLevels = 1;
            await db.SaveChangesAsync(Ct);
        }

        await NewGrain().AdministerAsync(false, "tester", "repair totals", "op-drift", Ct);

        await using var check = await _database.CreateDbContextAsync(Ct);
        var repaired = await check.AchievementProjections.SingleAsync(Ct);
        Assert.Equal(60, repaired.Score);
        Assert.Equal(6, repaired.EarnedLevels);
        var published = _fakes
            .Log.Calls.Where(x => x.Method == "SetAchievementTotalsAsync")
            .Select(x => ((int)x.Args[0]!, (int)x.Args[1]!))
            .ToList();
        Assert.Equal((60, 6), published[^1]);
    }

    [Fact]
    public async Task FactsAreBoundOnlyWhenTheyOccurredInsideTheWindow()
    {
        var from = DateTime.UtcNow.AddDays(-10);
        var until = DateTime.UtcNow.AddDays(-5);
        _catalog.Current =
        [
            Definition(100000) with
            {
                ActiveFromUtc = from,
                ActiveUntilUtc = until,
            },
        ];

        await RecordAtAsync("window-before", from.AddTicks(-1));
        await RecordAtAsync("window-at-start", from);
        await RecordAtAsync("window-last-tick", until.AddTicks(-1));
        await RecordAtAsync("window-at-end", until);

        await using var db = await _database.CreateDbContextAsync(Ct);
        var bound = (await db.AchievementFacts.ToListAsync(Ct)).Select(x => x.OperationId).Order();
        Assert.Equal(["window-at-start", "window-last-tick"], bound);
    }

    [Fact]
    public async Task AWindowedAchievementAppearsInsideItsWindowAndArchivesAfterIt()
    {
        var start = _clock.GetUtcNow().UtcDateTime;
        _catalog.Current =
        [
            Definition(100000) with
            {
                ActiveFromUtc = start.AddDays(1),
                ActiveUntilUtc = start.AddDays(2),
            },
        ];
        var grain = NewGrain();

        Assert.Empty(await grain.GetAchievementsAsync(Ct));

        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(
            AchievementState.Enabled,
            Assert.Single(await grain.GetAchievementsAsync(Ct)).State
        );

        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Empty(await grain.GetAchievementsAsync(Ct));
        _database.Insert(
            new AchievementProgressEntity
            {
                PlayerId = 1,
                AchievementId = 100000,
                Value = 1,
                EarnedLevel = 1,
            }
        );
        Assert.Equal(
            AchievementState.Archived,
            Assert.Single(await grain.GetAchievementsAsync(Ct)).State
        );
    }

    [Fact]
    public async Task ReconcileOpensNoAwardsOnceTheWindowHasClosed()
    {
        var start = _clock.GetUtcNow().UtcDateTime;
        _catalog.Current = [Definition(100000) with { ActiveUntilUtc = start.AddDays(1) }];
        _database.Insert(
            new AchievementProgressEntity
            {
                PlayerId = 1,
                AchievementId = 100000,
                Value = 3,
            }
        );
        _clock.Advance(TimeSpan.FromDays(1));

        await NewGrain().ReconcileAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.Equal(0, (await db.AchievementProgress.SingleAsync(Ct)).EarnedLevel);
    }

    [Fact]
    public async Task ChangingOnlyTheWindowMakesReconcileEvaluateThePlayerAgain()
    {
        _catalog.Current = [Definition(100000)];
        var grain = NewGrain();
        await grain.ReconcileAsync(Ct);
        Assert.Equal(1, NormalizeCalls());

        _catalog.Current =
        [
            Definition(100000) with
            {
                ActiveUntilUtc = DateTime.UtcNow.AddDays(30),
            },
        ];
        await grain.ReconcileAsync(Ct);

        Assert.Equal(2, NormalizeCalls());
    }

    private async Task RecordAtAsync(
        string operation,
        DateTime occurredAtUtc,
        string source = AchievementSources.FIGURE,
        string value = ""
    )
    {
        await using var db = await _database.CreateDbContextAsync(Ct);
        new AchievementFactRecorder(_catalog).Record(
            db,
            1,
            new()
            {
                OperationId = operation,
                Source = source,
                Value = value,
                OccurredAtUtc = occurredAtUtc,
            }
        );
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task LoginsCanCountDistinctUtcDaysAndRepeatsOnTheSameDayDoNotCount()
    {
        var day = DateTime.UtcNow.Date.AddDays(-10);
        _catalog.Current =
        [
            Definition(100000, AchievementSources.LOGIN) with
            {
                Reducer = AchievementReducer.Distinct,
                Match = new AchievementMatch { ValueFrom = AchievementValueSource.UtcDate },
            },
        ];

        await RecordAtAsync("login-1", day.AddHours(8), AchievementSources.LOGIN);
        await RecordAtAsync("login-2", day.AddHours(20), AchievementSources.LOGIN);
        await RecordAtAsync("login-3", day.AddDays(2).AddHours(1), AchievementSources.LOGIN);
        await RecordAtAsync("login-4", day.AddDays(3).AddHours(23), AchievementSources.LOGIN);
        await NewGrain().ProcessAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);
        Assert.Equal(3, progress.Value);
        Assert.Equal(3, progress.DistinctCount);
        Assert.Equal(3, progress.EarnedLevel);
        Assert.Equal(
            new[] { day, day.AddDays(2), day.AddDays(3) }.Select(x => x.ToString("yyyy-MM-dd")),
            await db
                .AchievementDistinctValues.OrderBy(x => x.Value)
                .Select(x => x.Value)
                .ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task LoginsCanBeCountedInTotal()
    {
        _catalog.Current = [Definition(100000, AchievementSources.LOGIN)];

        await RecordAtAsync("total-1", DateTime.UtcNow.AddDays(-3), AchievementSources.LOGIN);
        await RecordAtAsync("total-2", DateTime.UtcNow.AddDays(-3), AchievementSources.LOGIN);
        await RecordAtAsync("total-3", DateTime.UtcNow.AddDays(-1), AchievementSources.LOGIN);
        await NewGrain().ProcessAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.Equal(3, (await db.AchievementProgress.SingleAsync(Ct)).Value);
    }

    [Fact]
    public async Task AValueListCountsOnlyTheListedValuesAndBindsNothingElse()
    {
        _catalog.Current =
        [
            Definition(100000, AchievementSources.VISIT) with
            {
                Reducer = AchievementReducer.Distinct,
                Match = new AchievementMatch { Values = ["10", "20"] },
            },
        ];

        await RecordAtAsync("room-a", DateTime.UtcNow, AchievementSources.VISIT, "10");
        await RecordAtAsync("room-b", DateTime.UtcNow, AchievementSources.VISIT, "30");
        await RecordAtAsync("room-c", DateTime.UtcNow, AchievementSources.VISIT, "20");
        await RecordAtAsync("room-d", DateTime.UtcNow, AchievementSources.VISIT, "10");
        await NewGrain().ProcessAsync(Ct);

        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.Equal(2, (await db.AchievementProgress.SingleAsync(Ct)).DistinctCount);
        Assert.False(await db.AchievementFacts.AnyAsync(x => x.OperationId == "room-b", Ct));
    }

    [Fact]
    public async Task ADistinctFactWithoutAValueIsStillRefusedUnlessTheDateIsCounted()
    {
        _catalog.Current =
        [
            Definition(100000, AchievementSources.LOGIN) with
            {
                Reducer = AchievementReducer.Distinct,
            },
        ];

        var record = () => RecordAtAsync("empty-value", DateTime.UtcNow, AchievementSources.LOGIN);

        await Assert.ThrowsAsync<ArgumentException>(record);
    }

    [Fact]
    public async Task ANeverListenedToSourceStoresNoFactAtAll()
    {
        _catalog.Current = [Definition(100000, AchievementSources.RESPECT_GIVEN)];

        await RecordAtAsync("nobody-listens", DateTime.UtcNow, AchievementSources.FIGURE);

        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.False(await db.AchievementFacts.AnyAsync(Ct));
    }

    [Theory]
    [InlineData(AchievementState.Disabled)]
    [InlineData(AchievementState.Archived)]
    [InlineData(AchievementState.OffSeason)]
    public async Task OnlyAnAccruingAchievementMakesAFactWorthStoring(AchievementState state)
    {
        _catalog.Current = [Definition(100000) with { State = state }];

        await RecordAtAsync("not-accruing", DateTime.UtcNow);

        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.False(await db.AchievementFacts.AnyAsync(Ct));
    }

    [Fact]
    public async Task AListenedToFactIsStoredAsBefore()
    {
        _catalog.Current = [Definition(100000)];

        await RecordAtAsync("someone-listens", DateTime.UtcNow);

        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.Equal("someone-listens", (await db.AchievementFacts.SingleAsync(Ct)).OperationId);
    }

    [Fact]
    public async Task StateNobodyListenedToIsRecordedOnceADefinitionAppears()
    {
        _catalog.Current = [Definition(100000)];
        var grain = NewGrain();
        await grain.ReconcileAsync(Ct);
        Assert.Equal(0, await StateFactCountAsync());

        _catalog.Current =
        [
            Definition(100000),
            Definition(100001, AchievementSources.ACCOUNT_AGE) with
            {
                Reducer = AchievementReducer.Maximum,
            },
        ];
        await grain.ReconcileAsync(Ct);

        Assert.True(await StateFactCountAsync() > 0);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(x => x.AchievementId == 100001, Ct);
        Assert.True(progress.Value >= 10);
    }

    /// <summary>A definition on a source the state evaluator records, so its state facts are listened to.</summary>
    private static AchievementDefinition StateDefinition(int id) =>
        Definition(id, AchievementSources.ACCOUNT_AGE) with
        {
            Reducer = AchievementReducer.Maximum,
        };

    [Fact]
    public async Task TheTwelveDaysOfChristmasExampleAwardsAtThreeSixNineAndTwelveDays()
    {
        var example = ExampleDefinition("twelve-days-of-christmas");
        var from = DateTime.UtcNow.Date.AddDays(-30);
        var until = from.AddDays(12);
        _catalog.Current = [example with { ActiveFromUtc = from, ActiveUntilUtc = until }];

        await RecordAtAsync("xmas-early", from.AddDays(-1).AddHours(9), AchievementSources.LOGIN);
        await RecordAtAsync("xmas-d0-a", from.AddHours(8), AchievementSources.LOGIN);
        await RecordAtAsync("xmas-d0-b", from.AddHours(21), AchievementSources.LOGIN);
        await RecordAtAsync("xmas-d1", from.AddDays(1).AddHours(8), AchievementSources.LOGIN);
        await RecordAtAsync("xmas-d2", from.AddDays(2).AddHours(8), AchievementSources.LOGIN);
        await NewGrain().ProcessAsync(Ct);
        Assert.Equal((3, 1), await ProgressAsync());

        for (var day = 3; day < 12; day++)
            await RecordAtAsync(
                $"xmas-d{day}",
                from.AddDays(day).AddHours(8),
                AchievementSources.LOGIN
            );
        await RecordAtAsync("xmas-late", until.AddHours(1), AchievementSources.LOGIN);
        await NewGrain().ProcessAsync(Ct);

        Assert.Equal((12, 4), await ProgressAsync());
        await using var db = await _database.CreateDbContextAsync(Ct);
        Assert.False(
            await db.AchievementFacts.AnyAsync(
                x => x.OperationId == "xmas-early" || x.OperationId == "xmas-late",
                Ct
            )
        );
    }

    [Fact]
    public async Task TheChristmasRoomsExampleCountsOnlyTheListedRooms()
    {
        var example = ExampleDefinition("visit-the-christmas-rooms");
        var from = DateTime.UtcNow.Date.AddDays(-30);
        _catalog.Current =
        [
            example with
            {
                ActiveFromUtc = from,
                ActiveUntilUtc = from.AddDays(60),
            },
        ];

        foreach (
            var (operation, room) in new[]
            {
                ("r1", "1001"),
                ("r2", "1500"),
                ("r3", "1002"),
                ("r4", "1001"),
                ("r5", "1003"),
            }
        )
            await RecordAtAsync(operation, DateTime.UtcNow, AchievementSources.VISIT, room);
        await NewGrain().ProcessAsync(Ct);

        Assert.Equal((3, 2), await ProgressAsync());
    }

    [Fact]
    public void TheChristmasExampleRunsForExactlyTwelveDays()
    {
        var example = ExampleDefinition("twelve-days-of-christmas");

        Assert.Equal(new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc), example.ActiveFromUtc);
        Assert.Equal(TimeSpan.FromDays(12), example.ActiveUntilUtc - example.ActiveFromUtc);
        Assert.Equal([3, 6, 9, 12], example.Levels.Select(x => x.Requirement));
    }

    private async Task<(long Value, int EarnedLevel)> ProgressAsync()
    {
        await using var db = await _database.CreateDbContextAsync(Ct);
        var progress = await db.AchievementProgress.SingleAsync(Ct);

        return (progress.Value, progress.EarnedLevel);
    }

    private static AchievementDefinition ExampleDefinition(string name)
    {
        using var stream = typeof(AchievementProgressionTests).Assembly.GetManifestResourceStream(
            $"Examples.Achievements.{name}.json"
        );
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        return AchievementDefinitionJson.ReadAll(reader.ReadToEnd()).Single();
    }

    private async Task<int> StateFactCountAsync()
    {
        await using var db = await _database.CreateDbContextAsync(Ct);
        return await db.AchievementFacts.CountAsync(x => x.OperationId.StartsWith("state:"), Ct);
    }

    private int NormalizeCalls() =>
        _fakes.Log.Calls.Count(x =>
            x.Method == nameof(IPlayerBadgeGrain.NormalizeAchievementBadgesAsync)
        );

    private sealed class RecordingObserver : IAchievementObserver
    {
        private readonly SemaphoreSlim _arrived = new(0);
        private readonly ConcurrentQueue<AchievementLevelCompleted> _heard = new();

        public IReadOnlyCollection<AchievementLevelCompleted> Heard => _heard;

        public Task OnLevelCompletedAsync(AchievementLevelCompleted completed, CancellationToken ct)
        {
            _heard.Enqueue(completed);
            _arrived.Release();
            return Task.CompletedTask;
        }

        public async Task WaitForAsync(int count, CancellationToken ct)
        {
            for (var i = 0; i < count; i++)
                Assert.True(await _arrived.WaitAsync(TimeSpan.FromSeconds(10), ct));
        }
    }

    private sealed class ThrowingObserver : IAchievementObserver
    {
        public Task OnLevelCompletedAsync(
            AchievementLevelCompleted completed,
            CancellationToken ct
        ) => throw new InvalidOperationException("observer failure");
    }

    private sealed class TestCatalog : IAchievementCatalog
    {
        public ImmutableArray<AchievementDefinition> Current { get; set; } = [];

        public IDisposable RegisterSources(IEnumerable<AchievementSourceDefinition> sources) =>
            throw new NotSupportedException();

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ImportAsync(
            ImmutableArray<AchievementDefinition> definitions,
            bool apply,
            string actor,
            string reason,
            string operationId,
            CancellationToken ct
        ) => Task.CompletedTask;
    }
}
