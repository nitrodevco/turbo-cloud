using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Players;
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

namespace Turbo.Tests.Players;

public class WalletRewardNoticeTests : IDisposable
{
    private const int PLAYER_ID = 1;
    private const int CURRENCY_TYPE_ID = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TestCurrencyTypes _currencyTypes = new();
    private readonly TestNotices _notices = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WalletRewardNoticeTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER_ID,
                Name = "wallet-rewards",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = CURRENCY_TYPE_ID,
                Name = "credits",
                CurrencyType = CurrencyType.Credits,
                Enabled = true,
            }
        );
        _currencyTypes.Types[CURRENCY_TYPE_ID] = new CurrencyTypeSnapshot
        {
            Id = CURRENCY_TYPE_ID,
            Name = "credits",
            CurrencyType = CurrencyType.Credits,
            Enabled = true,
        };
        _fakes.Handlers[nameof(ICurrencyTypeProvider.GetCurrencyType)] = call =>
            _currencyTypes.Types.GetValueOrDefault((int)call.Args[0]!);
        _fakes.Handlers[nameof(ICurrencyTypeProvider.TryGetCurrencyTypeId)] = call =>
        {
            call.Args[1] = CURRENCY_TYPE_ID;
            return (CurrencyKind)call.Args[0]! == CurrencyKind.Credits;
        };
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task OfflineRewards_AggregateAndDeliverOnLoginWithoutCreditingAgain()
    {
        _notices.Delivery = PlayerNoticeDelivery.Offline;
        var wallet = await NewWalletAsync(Ct);

        (await wallet.CreditRewardAsync(CurrencyKind.Credits, 40, Ct)).Should().BeTrue();
        (await wallet.CreditRewardAsync(CurrencyKind.Credits, 15, Ct)).Should().BeTrue();

        await AssertCurrencyAsync(amount: 55, pending: 55);
        _notices.Rewards.Should().BeEmpty();

        _notices.Delivery = PlayerNoticeDelivery.Sent;
        wallet = await NewWalletAsync(Ct);
        await wallet.DeliverPendingRewardsAsync(Ct);

        _notices.Rewards.Should().ContainSingle().Which.Amount.Should().Be(55);
        await AssertCurrencyAsync(amount: 55, pending: 0);

        await wallet.DeliverPendingRewardsAsync(Ct);
        _notices.Rewards.Should().HaveCount(1);
        (await wallet.GetAmountForCurrencyAsync(CurrencyKind.Credits, Ct)).Should().Be(55);
    }

    [Theory]
    [InlineData(PlayerNoticeDelivery.Offline)]
    [InlineData(PlayerNoticeDelivery.Failed)]
    public async Task AnUndeliveredReward_RemainsPendingForRetry(PlayerNoticeDelivery result)
    {
        var wallet = await NewWalletAsync(Ct);
        _notices.Delivery = result;

        (await wallet.CreditRewardAsync(CurrencyKind.Credits, 23, Ct)).Should().BeTrue();
        await AssertCurrencyAsync(amount: 23, pending: 23);

        _notices.Delivery = PlayerNoticeDelivery.Sent;
        await wallet.DeliverPendingRewardsAsync(Ct);

        _notices.Rewards.Should().ContainSingle().Which.Amount.Should().Be(23);
        await AssertCurrencyAsync(amount: 23, pending: 0);
    }

    [Fact]
    public async Task AnOrdinaryCreditDoesNotCreateOrDeliverARewardReceipt()
    {
        var wallet = await NewWalletAsync(Ct);

        (await wallet.CreditAsync(CurrencyKind.Credits, 12, Ct)).Should().BeTrue();

        await AssertCurrencyAsync(amount: 12, pending: 0);
        _notices.Rewards.Should().BeEmpty();
    }

    [Fact]
    public async Task ARewardDeliveredImmediately_IsNotRepeatedOnLogin()
    {
        var wallet = await NewWalletAsync(Ct);
        _notices.Delivery = PlayerNoticeDelivery.Sent;

        (await wallet.CreditRewardAsync(CurrencyKind.Credits, 31, Ct)).Should().BeTrue();
        await wallet.DeliverPendingRewardsAsync(Ct);

        _notices.Rewards.Should().ContainSingle().Which.Amount.Should().Be(31);
        await AssertCurrencyAsync(amount: 31, pending: 0);
    }

    [Fact]
    public async Task InvalidAndOverflowingCredits_DoNotChangeBalanceOrReceipt()
    {
        var wallet = await NewWalletAsync(Ct);
        _notices.Delivery = PlayerNoticeDelivery.Offline;

        (await wallet.CreditRewardAsync(CurrencyKind.Credits, 0, Ct)).Should().BeFalse();
        (await wallet.CreditRewardAsync(CurrencyKind.Credits, -1, Ct)).Should().BeFalse();
        await using (var db = await _db.CreateDbContextAsync(Ct))
            (await db.PlayerCurrencies.CountAsync(Ct)).Should().Be(0);

        (await wallet.CreditRewardAsync(CurrencyKind.Credits, int.MaxValue, Ct)).Should().BeTrue();
        (await wallet.CreditRewardAsync(CurrencyKind.Credits, 1, Ct)).Should().BeFalse();

        await AssertCurrencyAsync(amount: int.MaxValue, pending: int.MaxValue);
    }

    private async Task<IPlayerWalletGrain> NewWalletAsync(CancellationToken ct)
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
        RoomHarness.SetField(grain, "_noticeService", _notices);

        await ((Orleans.Grain)grain).OnActivateAsync(ct);

        return (IPlayerWalletGrain)grain;
    }

    private async Task AssertCurrencyAsync(int amount, long pending)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = await db.PlayerCurrencies.SingleAsync();
        row.Amount.Should().Be(amount);
        row.PendingRewardAmount.Should().Be(pending);
    }

    private sealed class TestCurrencyTypes
    {
        public Dictionary<int, CurrencyTypeSnapshot> Types { get; } = [];
    }

    private sealed class TestNotices : IPlayerNoticeService
    {
        public PlayerNoticeDelivery Delivery { get; set; } = PlayerNoticeDelivery.Sent;
        public List<(long Amount, int CurrencyId)> Rewards { get; } = [];

        public Task<PlayerNoticeDelivery> SendCurrencyRewardAsync(
            PlayerId playerId,
            long amount,
            CurrencyTypeSnapshot currency,
            CancellationToken ct
        )
        {
            if (Delivery == PlayerNoticeDelivery.Sent)
                Rewards.Add((amount, currency.Id));

            return Task.FromResult(Delivery);
        }

        public Task<PlayerNoticeDelivery> SendAsync(
            PlayerId playerId,
            string textKey,
            string defaultText,
            IReadOnlyList<string> parameters,
            CancellationToken ct
        ) => Task.FromResult(Delivery);
    }
}
