using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Inventory;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementLeaderboardAndBadgeReplayTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AchievementLeaderboardAndBadgeReplayTests()
    {
        for (var id = 1; id <= 3; id++)
            _db.Insert(NewPlayer(id));

        _fakes.Handlers[nameof(IBadgeDirectoryGrain.GetInfoAsync)] = call =>
        {
            var codes = (ImmutableArray<string>)call.Args[0]!;
            return Task.FromResult(
                codes
                    .Select(code => new BadgeInfoSnapshot
                    {
                        BadgeCode = code,
                        OwnerCount = 1,
                        Rarity = BadgeRarityType.Common,
                    })
                    .ToImmutableArray()
            );
        };
        _fakes.Handlers[nameof(IBadgeDirectoryGrain.GetRequestableBadgeAsync)] = _ => null;
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LeaderboardTypeTwoRanksEarnedLevelsSeparatelyFromBadgeCountsAndAchievementScore()
    {
        _db.Insert(NewBadge(1, 1, "ACH_P1_A"));
        _db.Insert(NewBadge(2, 1, "ACH_P1_B"));
        _db.Insert(NewBadge(3, 1, "ACH_P1_C"));
        _db.Insert(NewBadge(4, 2, "ACH_P2_A"));
        _db.Insert(
            new AchievementProjectionEntity
            {
                PlayerId = 1,
                Score = 900,
                EarnedLevels = 2,
            }
        );
        _db.Insert(
            new AchievementProjectionEntity
            {
                PlayerId = 2,
                Score = 100,
                EarnedLevels = 5,
            }
        );
        // No definitions are present: the board is still the durable all-time earned-level total.

        var grain = NewLeaderboardGrain();
        var levels = await grain.GetLeaderboardAsync(
            BadgeLeaderboardType.AchievementLevel,
            rarity: 0,
            chunkIndex: 0,
            chunkSize: 10,
            forPlayerId: 1,
            Ct
        );
        var badges = await grain.GetLeaderboardAsync(
            BadgeLeaderboardType.TotalBadges,
            rarity: 0,
            chunkIndex: 0,
            chunkSize: 10,
            forPlayerId: 1,
            Ct
        );

        levels.Type.Should().Be(BadgeLeaderboardType.AchievementLevel);
        levels
            .Entries.Select(x => (x.PlayerId.Value, x.Score, x.Rank))
            .Should()
            .Equal((2, 5, 1), (1, 2, 2));
        levels.OwnEntry.Should().NotBeNull();
        levels.OwnEntry!.Score.Should().Be(2);
        badges
            .Entries.Select(x => (x.PlayerId.Value, x.Score, x.Rank))
            .Should()
            .Equal((1, 3, 1), (2, 1, 2));
    }

    [Fact]
    public async Task ReplayingAchievementEntitlementKeepsWornBadgeAndSlot()
    {
        _db.Insert(
            new PlayerBadgeEntity
            {
                Id = 1,
                PlayerEntityId = 1,
                BadgeCode = "ACH_ManualWorn",
                SlotId = 3,
                PlayerEntity = null!,
            }
        );
        var badges = await NewBadgeGrainAsync();

        await badges.GrantAchievementAsync(20, 1, "ACH_ManualWorn", Ct);
        await badges.GrantAchievementAsync(20, 1, "ACH_ManualWorn", Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var badge = await db.PlayerBadges.SingleAsync(Ct);
        badge.SlotId.Should().Be(3);
    }

    [Fact]
    public async Task ReplayingWornAchievementUpgradeKeepsTransferredSlotAndSingleBadge()
    {
        var badges = await NewBadgeGrainAsync();
        await badges.GrantAchievementAsync(30, 1, "ACH_Worn1", Ct);
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var oldBadge = await db.PlayerBadges.SingleAsync(Ct);
            oldBadge.SlotId = 4;
            await db.SaveChangesAsync(Ct);
        }

        await badges.GrantAchievementAsync(30, 2, "ACH_Worn2", Ct);
        await badges.GrantAchievementAsync(30, 2, "ACH_Worn2", Ct);

        await using var check = await _db.CreateDbContextAsync(Ct);
        var badge = await check.PlayerBadges.SingleAsync(Ct);
        badge.BadgeCode.Should().Be("ACH_Worn2");
        badge.SlotId.Should().Be(4);
    }

    [Fact]
    public async Task UpgradingReplacesEveryLowerLevelAndTheWornLevelKeepsItsSlot()
    {
        _db.Insert(NewBadge(11, 1, "ACH_TrueHabbo1", slot: 2));
        _db.Insert(NewBadge(12, 1, "ACH_TrueHabbo2"));
        _db.Insert(NewBadge(13, 1, "ACH_Unrelated2"));
        var badges = await NewBadgeGrainAsync();

        await badges.GrantAchievementAsync(40, 3, "ACH_TrueHabbo3", Ct);
        await badges.GrantAchievementAsync(40, 3, "ACH_TrueHabbo3", Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var owned = await db.PlayerBadges.OrderBy(x => x.BadgeCode).ToListAsync(Ct);
        owned.Select(x => x.BadgeCode).Should().Equal("ACH_TrueHabbo3", "ACH_Unrelated2");
        owned[0].SlotId.Should().Be(2);
    }

    [Fact]
    public async Task AHigherLevelAlreadyOwnedIsKeptAndTheGrantedLowerLevelIsNotAdded()
    {
        _db.Insert(NewBadge(21, 1, "ACH_TrueHabbo5", slot: 1));
        var badges = await NewBadgeGrainAsync();

        await badges.GrantAchievementAsync(40, 2, "ACH_TrueHabbo2", Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var badge = await db.PlayerBadges.SingleAsync(Ct);
        badge.BadgeCode.Should().Be("ACH_TrueHabbo5");
        badge.SlotId.Should().Be(1);
    }

    [Fact]
    public async Task NormalizingCollapsesLegacyOwnedLevelsToTheHighestAndKeepsTheWornSlot()
    {
        for (var level = 1; level <= 10; level++)
            _db.Insert(
                NewBadge(100 + level, 1, $"ACH_TrueHabbo{level}", slot: level == 1 ? 5 : null)
            );
        _db.Insert(NewBadge(200, 1, "ACH_TrueHabboExtra2"));
        var badges = await NewBadgeGrainAsync();

        await badges.NormalizeAchievementBadgesAsync(["ACH_TrueHabbo"], Ct);
        await badges.NormalizeAchievementBadgesAsync(["ACH_TrueHabbo"], Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var owned = await db.PlayerBadges.OrderBy(x => x.BadgeCode).ToListAsync(Ct);
        owned.Select(x => x.BadgeCode).Should().Equal("ACH_TrueHabbo10", "ACH_TrueHabboExtra2");
        owned[0].SlotId.Should().Be(5);
    }

    private IBadgeLeaderboardGrain NewLeaderboardGrain()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Badges.BadgeLeaderboardGrain",
            _fakes,
            _db
        );
        return (IBadgeLeaderboardGrain)grain;
    }

    private async Task<IPlayerBadgeGrain> NewBadgeGrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.Badges.PlayerBadgeGrain",
            _fakes,
            _db,
            playerId: 1
        );
        await ((Grain)grain).OnActivateAsync(Ct);
        return (IPlayerBadgeGrain)grain;
    }

    private static PlayerEntity NewPlayer(int id) =>
        new()
        {
            Id = id,
            Name = $"achievement-board-{id}",
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static PlayerBadgeEntity NewBadge(
        int id,
        int playerId,
        string code,
        int? slot = null
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = playerId,
            BadgeCode = code,
            SlotId = slot,
            PlayerEntity = null!,
        };
}
