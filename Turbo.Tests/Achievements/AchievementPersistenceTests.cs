using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Inventory.Grains.Badges;
using Turbo.Players.Grains;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementPersistenceTests : IDisposable
{
    private const int PLAYER_ID = 1;
    private const int CURRENCY_TYPE_ID = 1;
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly MovementCause Cause = MovementCause.New("test", 0);

    public AchievementPersistenceTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER_ID,
                Name = "achievement-persistence",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _fakes.Handlers[nameof(IBadgeDirectoryGrain.GetInfoAsync)] = call =>
        {
            var codes = (ImmutableArray<string>)call.Args[0]!;
            return Task.FromResult(
                ImmutableArray.CreateRange(
                    codes.Select(code => new BadgeInfoSnapshot
                    {
                        BadgeCode = code,
                        OwnerCount = 1,
                        Rarity = BadgeRarityType.Common,
                    })
                )
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
    public async Task ReplayingAnAchievementWalletReceiptDoesNotCreditTwiceAndRejectsPayloadChanges()
    {
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = CURRENCY_TYPE_ID,
                Name = "credits",
                CurrencyType = CurrencyType.Credits,
                Enabled = true,
            }
        );
        var currency = new CurrencyTypeSnapshot
        {
            Id = CURRENCY_TYPE_ID,
            Name = "credits",
            CurrencyType = CurrencyType.Credits,
            Enabled = true,
        };
        _fakes.Handlers[nameof(ICurrencyTypeProvider.TryGetCurrencyTypeId)] = call =>
        {
            call.Args[1] = CURRENCY_TYPE_ID;
            return (CurrencyKind)call.Args[0]! == CurrencyKind.Credits;
        };
        _fakes.Handlers[nameof(ICurrencyTypeProvider.GetCurrencyType)] = _ => currency;
        _fakes.Handlers[nameof(IPlayerNoticeService.SendCurrencyRewardAsync)] = _ =>
            Task.FromResult(PlayerNoticeDelivery.Offline);

        var wallet = (await NewWalletAsync()).Wallet;
        (await wallet.CreditAchievementAsync("award-1", CurrencyKind.Credits, 25, Cause, Ct))
            .Should()
            .BeTrue();
        (await wallet.CreditAchievementAsync("award-1", CurrencyKind.Credits, 25, Cause, Ct))
            .Should()
            .BeTrue();

        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            (await db.PlayerCurrencies.SingleAsync(Ct)).Amount.Should().Be(25);
            (await db.PlayerCurrencies.SingleAsync(Ct)).PendingRewardAmount.Should().Be(25);
            (await db.AchievementWalletReceipts.CountAsync(Ct)).Should().Be(1);
        }

        var act = () =>
            wallet.CreditAchievementAsync("award-1", CurrencyKind.Credits, 26, Cause, Ct);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AchievementBadgeUpgradeLeavesAnotherAchievementsBadgeAlone()
    {
        var (_, badgeGrain) = await NewBadgeGrainAsync();

        await badgeGrain.GrantAchievementAsync(10, 1, Levels("ACH_Upgrade", 10), Ct);
        await badgeGrain.GrantAchievementAsync(20, 1, Levels("ACH_Other", 10), Ct);
        await badgeGrain.GrantAchievementAsync(10, 2, Levels("ACH_Upgrade", 10), Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerBadges.Select(x => x.BadgeCode).OrderBy(x => x).ToListAsync(Ct))
            .Should()
            .Equal("ACH_Other1", "ACH_Upgrade2");
    }

    [Fact]
    public async Task ABadgePrefixEndingInADigitStillReplacesItsLowerLevels()
    {
        var (_, badgeGrain) = await NewBadgeGrainAsync();
        var bazaar17 = Levels("ACH_bazaar17", 10);

        // Habbo's bazaar17: ACH_bazaar171 .. ACH_bazaar1710 (not ACH_bazaar + 171).
        await badgeGrain.GrantAchievementAsync(10, 1, bazaar17, Ct);
        await badgeGrain.GrantAchievementAsync(10, 2, bazaar17, Ct);

        await using (var db = await _db.CreateDbContextAsync(Ct))
            (await db.PlayerBadges.Select(x => x.BadgeCode).ToListAsync(Ct))
                .Should()
                .Equal("ACH_bazaar172");

        await badgeGrain.GrantAchievementAsync(10, 10, bazaar17, Ct);
        await badgeGrain.GrantAchievementAsync(10, 3, bazaar17, Ct);

        await using (var db = await _db.CreateDbContextAsync(Ct))
            (await db.PlayerBadges.Select(x => x.BadgeCode).ToListAsync(Ct))
                .Should()
                .Equal("ACH_bazaar1710");
    }

    [Fact]
    public async Task TwoAchievementsWhoseCodesReadAsLevelsOfOneBadgeKeepBothBadges()
    {
        var (_, badgeGrain) = await NewBadgeGrainAsync();

        // Habbo's Profile1 and Profile2 have one level each, ACH_Profile1 and ACH_Profile2
        // (official client capture, Achievements list 2026-10-15): not levels 1 and 2 of one badge.
        await badgeGrain.GrantAchievementAsync(10282, 1, ["ACH_Profile1"], Ct);
        await badgeGrain.GrantAchievementAsync(10283, 1, ["ACH_Profile2"], Ct);
        await badgeGrain.NormalizeAchievementBadgesAsync(
            [
                ["ACH_Profile1"],
                ["ACH_Profile2"],
            ],
            Ct
        );

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerBadges.Select(x => x.BadgeCode).OrderBy(x => x).ToListAsync(Ct))
            .Should()
            .Equal("ACH_Profile1", "ACH_Profile2");
    }

    [Fact]
    public async Task ALevelReplacesTheAchievementsOwnLowerLevelCodesWhateverTheyEndIn()
    {
        _db.Insert(
            new PlayerBadgeEntity
            {
                PlayerEntityId = PLAYER_ID,
                BadgeCode = "ACH_Profile1",
                SlotId = 2,
                PlayerEntity = null!,
            }
        );
        var (_, badgeGrain) = await NewBadgeGrainAsync();
        ImmutableArray<string> levels = ["ACH_Profile1", "ACH_ProfileGold"];

        await badgeGrain.GrantAchievementAsync(10, 2, levels, Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var badge = await db.PlayerBadges.SingleAsync(Ct);
        badge.BadgeCode.Should().Be("ACH_ProfileGold");
        badge.SlotId.Should().Be(2);
    }

    [Fact]
    public async Task RemovingAnAchievementBadgeRemovesItLikeAnyOtherBadge()
    {
        var (_, badgeGrain) = await NewBadgeGrainAsync();
        await badgeGrain.GrantAchievementAsync(30, 1, Levels("ACH_Earned", 10), Ct);

        (await badgeGrain.RemoveBadgeAsync("ACH_Earned1", Ct)).Should().BeTrue();

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerBadges.CountAsync(Ct)).Should().Be(0);
    }

    private async Task<(IPlayerWalletGrain Wallet, object Grain)> NewWalletAsync()
    {
        var grain = GrainHarness.Create<PlayerWalletGrain>(_fakes, _db, PLAYER_ID);
        GrainHarness.Replace<ICurrencyTypeProvider>(grain, _fakes.Create<ICurrencyTypeProvider>());
        GrainHarness.Replace<IPlayerNoticeService>(grain, _fakes.Create<IPlayerNoticeService>());
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);
        return ((IPlayerWalletGrain)grain, grain);
    }

    /// <summary>An achievement's level codes: the prefix plus each level number.</summary>
    private static ImmutableArray<string> Levels(string prefix, int count) =>
        [.. Enumerable.Range(1, count).Select(x => prefix + x)];

    private async Task<(object Grain, IPlayerBadgeGrain Badges)> NewBadgeGrainAsync()
    {
        var grain = GrainHarness.Create<PlayerBadgeGrain>(_fakes, _db, PLAYER_ID);
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);
        return (grain, (IPlayerBadgeGrain)grain);
    }
}
