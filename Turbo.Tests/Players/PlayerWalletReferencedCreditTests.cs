using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Players;
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

public sealed class PlayerWalletReferencedCreditTests : IDisposable
{
    private const int PLAYER_ID = 1;
    private const int OTHER_PLAYER_ID = 2;
    private const int CURRENCY_TYPE_ID = 1;
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerWalletReferencedCreditTests()
    {
        foreach (var id in new[] { PLAYER_ID, OTHER_PLAYER_ID })
        {
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"wallet-credit-{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
        }
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
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankReferenceIsRejected(string reference)
    {
        var wallet = await NewWalletAsync(PLAYER_ID);

        var act = () => wallet.CreditAsync(CurrencyKind.Credits, 10, reference, Ct);

        await act.Should().ThrowAsync<ArgumentException>();
        (await BalanceAsync(PLAYER_ID)).Should().Be(0);
    }

    [Fact]
    public async Task ReferenceLongerThanTheLimitIsRejected()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);
        var tooLong = new string('x', WalletCreditReference.MaxLength + 1);

        var act = () => wallet.CreditAsync(CurrencyKind.Credits, 10, tooLong, Ct);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await BalanceAsync(PLAYER_ID)).Should().Be(0);
    }

    [Fact]
    public async Task ReferenceAtTheLimitIsAccepted()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);
        var longest = new string('x', WalletCreditReference.MaxLength);

        (await wallet.CreditAsync(CurrencyKind.Credits, 10, longest, Ct))
            .Should()
            .Be(WalletCreditResult.Applied);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task NonPositiveAmountIsRejectedAndLeavesNoReceipt(int amount)
    {
        var wallet = await NewWalletAsync(PLAYER_ID);

        (await wallet.CreditAsync(CurrencyKind.Credits, amount, "plugin:1", Ct))
            .Should()
            .Be(WalletCreditResult.Rejected);

        (await BalanceAsync(PLAYER_ID)).Should().Be(0);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.WalletCreditReceipts.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task OverflowIsRejectedAndLeavesNoReceipt()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);
        await wallet.CreditAsync(CurrencyKind.Credits, int.MaxValue, "plugin:max", Ct);

        (await wallet.CreditAsync(CurrencyKind.Credits, 1, "plugin:over", Ct))
            .Should()
            .Be(WalletCreditResult.Rejected);

        (await BalanceAsync(PLAYER_ID)).Should().Be(int.MaxValue);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.WalletCreditReceipts.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task FirstCreditAppliesAndRepeatedReferenceIsANoOp()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);

        (await wallet.CreditAsync(CurrencyKind.Credits, 25, "plugin:order-1", Ct))
            .Should()
            .Be(WalletCreditResult.Applied);
        (await wallet.CreditAsync(CurrencyKind.Credits, 25, "plugin:order-1", Ct))
            .Should()
            .Be(WalletCreditResult.AlreadyApplied);

        (await BalanceAsync(PLAYER_ID)).Should().Be(25);
        (await wallet.GetAmountForCurrencyAsync(CurrencyKind.Credits, Ct)).Should().Be(25);
    }

    [Fact]
    public async Task RepeatedReferenceIsANoOpAfterReactivation()
    {
        var first = await NewWalletAsync(PLAYER_ID);
        await first.CreditAsync(CurrencyKind.Credits, 25, "plugin:order-1", Ct);

        var second = await NewWalletAsync(PLAYER_ID);

        (await second.CreditAsync(CurrencyKind.Credits, 25, "plugin:order-1", Ct))
            .Should()
            .Be(WalletCreditResult.AlreadyApplied);
        (await BalanceAsync(PLAYER_ID)).Should().Be(25);
    }

    [Fact]
    public async Task DifferentReferenceApplies()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);

        await wallet.CreditAsync(CurrencyKind.Credits, 25, "plugin:order-1", Ct);
        (await wallet.CreditAsync(CurrencyKind.Credits, 5, "plugin:order-2", Ct))
            .Should()
            .Be(WalletCreditResult.Applied);

        (await BalanceAsync(PLAYER_ID)).Should().Be(30);
        (await wallet.GetAmountForCurrencyAsync(CurrencyKind.Credits, Ct)).Should().Be(30);
    }

    [Fact]
    public async Task ReferenceIsScopedPerPlayer()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);
        var other = await NewWalletAsync(OTHER_PLAYER_ID);

        (await wallet.CreditAsync(CurrencyKind.Credits, 10, "plugin:order-1", Ct))
            .Should()
            .Be(WalletCreditResult.Applied);
        (await other.CreditAsync(CurrencyKind.Credits, 10, "plugin:order-1", Ct))
            .Should()
            .Be(WalletCreditResult.Applied);

        (await BalanceAsync(PLAYER_ID)).Should().Be(10);
        (await BalanceAsync(OTHER_PLAYER_ID)).Should().Be(10);
    }

    [Fact]
    public async Task ConcurrentCreditsWithOneReferenceApplyOnce()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);
        await wallet.CreditAsync(CurrencyKind.Credits, 1, "plugin:seed", Ct);

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 5)
                .Select(_ => wallet.CreditAsync(CurrencyKind.Credits, 10, "plugin:order-1", Ct))
        );

        results.Count(x => x == WalletCreditResult.Applied).Should().Be(1);
        results.Count(x => x == WalletCreditResult.AlreadyApplied).Should().Be(4);
        (await BalanceAsync(PLAYER_ID)).Should().Be(11);
    }

    [Fact]
    public async Task ExistingCreditOverloadIsUnchangedAndLeavesNoReceipt()
    {
        var wallet = await NewWalletAsync(PLAYER_ID);

        (await wallet.CreditAsync(CurrencyKind.Credits, 10, Ct)).Should().BeTrue();
        (await wallet.CreditAsync(CurrencyKind.Credits, 10, Ct)).Should().BeTrue();

        (await BalanceAsync(PLAYER_ID)).Should().Be(20);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.WalletCreditReceipts.CountAsync(Ct)).Should().Be(0);
    }

    private async Task<int> BalanceAsync(int playerId)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        return await db
            .PlayerCurrencies.Where(x => x.PlayerEntityId == playerId)
            .Select(x => x.Amount)
            .SingleOrDefaultAsync(Ct);
    }

    private async Task<IPlayerWalletGrain> NewWalletAsync(int playerId)
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerWalletGrain",
            _fakes,
            _db,
            playerId
        );
        RoomHarness.SetField(
            grain,
            "_currencyTypeProvider",
            _fakes.Create<ICurrencyTypeProvider>()
        );
        RoomHarness.SetField(grain, "_noticeService", _fakes.Create<IPlayerNoticeService>());
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);
        return (IPlayerWalletGrain)grain;
    }
}
