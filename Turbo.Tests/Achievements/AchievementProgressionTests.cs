using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Turbo.Achievements;
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
        RoomHarness.SetField(
            grain,
            "_evaluator",
            new AchievementStateEvaluator(_database, recorder)
        );
        return (IPlayerAchievementGrain)grain;
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
        var awards = await db.AchievementAwards.OrderBy(x => x.Level).ToListAsync(Ct);
        Assert.Equal([1, 2, 3], awards.Select(x => x.Level));
        Assert.All(
            awards,
            award =>
            {
                Assert.True(award.Completed);
                Assert.False(award.Presented);
            }
        );
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
    public async Task BlockedAchievementDoesNotPreventAnotherAchievementAndRetryPreservesFrozenAwards()
    {
        _catalog.Current = [Definition(100000), Definition(100001)];
        _fakes.Handlers[nameof(IPlayerBadgeGrain.GrantAchievementAsync)] = call =>
            (int)call.Args[0]! == 100000
                ? Task.FromException(new IOException("temporary badge failure"))
                : Task.CompletedTask;
        await RecordAsync("action-2", AchievementSources.FIGURE, 3);
        await NewGrain().ProcessAsync(Ct);
        await using (var db = await _database.CreateDbContextAsync(Ct))
        {
            Assert.Equal(3, await db.AchievementAwards.CountAsync(x => x.Completed, Ct));
            Assert.Equal(30, (await db.AchievementProjections.SingleAsync(Ct)).Score);
            Assert.NotNull(
                (
                    await db.AchievementAwards.SingleAsync(
                        x => x.AchievementId == 100000 && x.Level == 1,
                        Ct
                    )
                ).BlockedReason
            );
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
            Assert.Equal(6, await db.AchievementAwards.CountAsync(x => x.Completed, Ct));
            Assert.Equal(60, (await db.AchievementProjections.SingleAsync(Ct)).Score);
            Assert.Equal(6, (await db.AchievementProjections.SingleAsync(Ct)).EarnedLevels);
        }
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
            Assert.Empty(await check.AchievementAwards.ToListAsync(Ct));

        _database.Insert(
            new AchievementMembershipIntervalEntity
            {
                PlayerId = 1,
                StartUtc = DateTime.UtcNow.AddMinutes(-1),
                EndUtc = DateTime.UtcNow.AddDays(31),
            }
        );
        await NewGrain().ReconcileAsync(Ct);
        await NewGrain().ReconcileAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var award = Assert.Single(await db.AchievementAwards.ToListAsync(Ct));
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
        _database.Insert(
            new AchievementMembershipIntervalEntity
            {
                PlayerId = 1,
                StartUtc = DateTime.UtcNow.AddDays(-2),
                EndUtc = DateTime.UtcNow.AddDays(31),
            }
        );
        await NewGrain().ReconcileAsync(Ct);
        string frozenDefinition;
        string frozenReward;
        await using (var before = await _database.CreateDbContextAsync(Ct))
        {
            var award = await before.AchievementAwards.SingleAsync(Ct);
            frozenDefinition = award.DefinitionJson;
            frozenReward = award.RewardJson;
        }
        _catalog.Current = [AchievementDefaults.Definitions.Single(x => x.Key == "hc-duration")];
        await NewGrain().ReconcileAsync(Ct);
        await using var db = await _database.CreateDbContextAsync(Ct);
        var retained = Assert.Single(await db.AchievementAwards.ToListAsync(Ct));
        Assert.Equal(frozenDefinition, retained.DefinitionJson);
        Assert.Equal(frozenReward, retained.RewardJson);
        Assert.Equal(1, retained.Revision);
        Assert.True((await db.AchievementProgress.SingleAsync(Ct)).Value >= 2 * 86400);
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
        var awards = await db.AchievementAwards.OrderBy(x => x.Level).ToListAsync(Ct);
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
        Assert.Empty(await db.AchievementAwards.ToListAsync(Ct));
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
        Assert.Empty(await db.AchievementStateValues.ToListAsync(Ct));
    }

    [Fact]
    public async Task ReconcileDoesCatalogWorkOnlyWhenTheCatalogRevisionsChange()
    {
        _catalog.Current = [Definition(100000)];
        var grain = NewGrain();
        await grain.ReconcileAsync(Ct);
        await using (var db = await _database.CreateDbContextAsync(Ct))
            Assert.NotEqual("", (await db.AchievementProjections.SingleAsync(Ct)).ReconciledStamp);
        var stateFacts = await StateFactCountAsync();

        await grain.ReconcileAsync(Ct);

        Assert.Equal(1, NormalizeCalls());
        Assert.Equal(stateFacts, await StateFactCountAsync());
        _catalog.Current = [Definition(100000) with { Revision = 2 }];
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

    private async Task<int> StateFactCountAsync()
    {
        await using var db = await _database.CreateDbContextAsync(Ct);
        return await db.AchievementFacts.CountAsync(x => x.OperationId.StartsWith("state:"), Ct);
    }

    private int NormalizeCalls() =>
        _fakes.Log.Calls.Count(x =>
            x.Method == nameof(IPlayerBadgeGrain.NormalizeAchievementBadgesAsync)
        );

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
