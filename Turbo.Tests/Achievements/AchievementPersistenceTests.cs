using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Inventory;
using Turbo.Players;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
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
        (await wallet.CreditAchievementAsync("award-1", CurrencyKind.Credits, 25, Ct))
            .Should()
            .BeTrue();
        (await wallet.CreditAchievementAsync("award-1", CurrencyKind.Credits, 25, Ct))
            .Should()
            .BeTrue();

        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            (await db.PlayerCurrencies.SingleAsync(Ct)).Amount.Should().Be(25);
            (await db.PlayerCurrencies.SingleAsync(Ct)).PendingRewardAmount.Should().Be(25);
            (await db.AchievementWalletReceipts.CountAsync(Ct)).Should().Be(1);
        }

        var act = () => wallet.CreditAchievementAsync("award-1", CurrencyKind.Credits, 26, Ct);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AchievementBadgeUpgradePreservesBadgeSharedByAnotherEntitlement()
    {
        var (_, badgeGrain) = await NewBadgeGrainAsync();

        await badgeGrain.GrantAchievementAsync(
            achievementId: 10,
            level: 1,
            badgeCode: "ACH_Shared1",
            ct: Ct
        );
        await badgeGrain.GrantAchievementAsync(
            achievementId: 20,
            level: 1,
            badgeCode: "ACH_Shared1",
            ct: Ct
        );
        await badgeGrain.GrantAchievementAsync(
            achievementId: 10,
            level: 2,
            badgeCode: "ACH_Upgrade2",
            ct: Ct
        );

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerBadges.Select(x => x.BadgeCode).OrderBy(x => x).ToListAsync(Ct))
            .Should()
            .Equal("ACH_Shared1", "ACH_Upgrade2");
        (
            await db
                .AchievementBadgeEntitlements.OrderBy(x => x.AchievementId)
                .Select(x => new
                {
                    x.AchievementId,
                    x.Level,
                    x.BadgeCode,
                })
                .ToListAsync(Ct)
        )
            .Should()
            .BeEquivalentTo([
                new
                {
                    AchievementId = 10,
                    Level = 2,
                    BadgeCode = "ACH_Upgrade2",
                },
                new
                {
                    AchievementId = 20,
                    Level = 1,
                    BadgeCode = "ACH_Shared1",
                },
            ]);
    }

    [Fact]
    public async Task RemovingAnAchievementBadgeRemovesItLikeAnyOtherBadge()
    {
        var (_, badgeGrain) = await NewBadgeGrainAsync();
        await badgeGrain.GrantAchievementAsync(30, 1, "ACH_Earned1", Ct);

        (await badgeGrain.RemoveBadgeAsync("ACH_Earned1", Ct)).Should().BeTrue();

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerBadges.CountAsync(Ct)).Should().Be(0);
    }

    private async Task<(IPlayerWalletGrain Wallet, object Grain)> NewWalletAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerWalletGrain",
            _fakes,
            _db,
            PLAYER_ID
        );
        RoomHarness.SetField(
            grain,
            "_currencyTypeProvider",
            _fakes.Create<ICurrencyTypeProvider>()
        );
        RoomHarness.SetField(grain, "_noticeService", _fakes.Create<IPlayerNoticeService>());
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);
        return ((IPlayerWalletGrain)grain, grain);
    }

    private async Task<(object Grain, IPlayerBadgeGrain Badges)> NewBadgeGrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.Badges.PlayerBadgeGrain",
            _fakes,
            _db,
            PLAYER_ID
        );
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);
        return (grain, (IPlayerBadgeGrain)grain);
    }
}
