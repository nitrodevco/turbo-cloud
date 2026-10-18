using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Movements;
using Turbo.Players;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Orleans;
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

/// <summary>
/// The wallet's movements (docs/movements.md): while recording, every currency a wallet method
/// moves is one movement under the caller's cause, committed with the balance; a refused debit,
/// a replayed credit and a recording that is off write none, and the balances move as before.
/// </summary>
public sealed class PlayerWalletMovementTests : IDisposable
{
    private const int PLAYER_ID = 1;
    private const int CREDITS_TYPE_ID = 1;
    private const int DIAMONDS_TYPE_ID = 2;
    private const int DIAMONDS = 5;

    private static readonly CurrencyKind Diamonds = CurrencyKind.ActivityPoints(DIAMONDS);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TestOptionsMonitor<MovementsConfig> _config = new(new() { Enabled = true });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerWalletMovementTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER_ID,
                Name = "wallet-movements",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

        var types = new Dictionary<int, CurrencyTypeSnapshot>
        {
            [CREDITS_TYPE_ID] = new()
            {
                Id = CREDITS_TYPE_ID,
                Name = "credits",
                CurrencyType = CurrencyType.Credits,
                Enabled = true,
            },
            [DIAMONDS_TYPE_ID] = new()
            {
                Id = DIAMONDS_TYPE_ID,
                Name = "diamonds",
                CurrencyType = CurrencyType.ActivityPoints,
                ActivityPointType = DIAMONDS,
                Enabled = true,
            },
        };

        foreach (var type in types.Values)
            _db.Insert(
                new CurrencyTypeEntity
                {
                    Id = type.Id,
                    Name = type.Name,
                    CurrencyType = type.CurrencyType,
                    ActivityPointType = type.ActivityPointType,
                    Enabled = true,
                }
            );

        _fakes.Handlers[nameof(ICurrencyTypeProvider.TryGetCurrencyTypeId)] = call =>
        {
            var kind = (CurrencyKind)call.Args[0]!;
            var type = types.Values.FirstOrDefault(x =>
                x.CurrencyType == kind.CurrencyType && x.ActivityPointType == kind.ActivityPointType
            );
            call.Args[1] = type?.Id ?? 0;
            return type is not null;
        };
        _fakes.Handlers[nameof(ICurrencyTypeProvider.GetCurrencyType)] = call =>
            types.GetValueOrDefault((int)call.Args[0]!);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Debit_WritesOneMovementWithTheBalance()
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();
        var cause = MovementCause.New(MovementCauses.CATALOG_PURCHASE, PLAYER_ID, 7);

        (await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 30)], cause, Ct))
            .Succeeded.Should()
            .BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(70);
        var row = Movements().Should().ContainSingle().Subject;
        row.OperationId.Should().Be(cause.OperationId);
        row.Seq.Should().Be(0);
        row.Cause.Should().Be(MovementCauses.CATALOG_PURCHASE);
        row.CauseRef.Should().Be("7");
        row.Actor.Should().Be(PLAYER_ID);
        row.AssetKind.Should().Be(AssetKind.Currency);
        row.AssetId.Should().Be(0);
        row.CurrencyType.Should().Be(CurrencyType.Credits);
        row.ActivityPointType.Should().BeNull();
        row.Amount.Should().Be(30);
        (row.FromOwner, row.FromKind).Should().Be((PLAYER_ID, PlaceKind.Wallet));
        (row.ToOwner, row.ToKind).Should().Be((0, PlaceKind.None));
    }

    [Fact]
    public async Task Debit_OfTwoCurrencies_IsOneOperationNumberedFromTheCausesFirstSeq()
    {
        Seed(credits: 100, diamonds: 10);
        var wallet = await NewWalletAsync();
        var cause = MovementCause
            .New(MovementCauses.WIRED_CHEST_UPGRADE, PLAYER_ID, 3)
            .StartingAt(2);

        (
            await wallet.TryDebitAsync(
                [Debit(CurrencyKind.Credits, 30), Debit(Diamonds, 4)],
                cause,
                Ct
            )
        )
            .Succeeded.Should()
            .BeTrue();

        (Balance(CREDITS_TYPE_ID), Balance(DIAMONDS_TYPE_ID)).Should().Be((70, 6));
        var rows = Movements();
        rows.Select(x => (x.OperationId, x.Seq, x.CurrencyType, x.ActivityPointType, x.Amount))
            .Should()
            .Equal(
                (cause.OperationId, (short)2, CurrencyType.Credits, (int?)null, 30L),
                (cause.OperationId, (short)3, CurrencyType.ActivityPoints, DIAMONDS, 4L)
            );
    }

    [Fact]
    public async Task Debit_TheBalanceCannotCover_WritesNone()
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();

        (await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 101)], Cause(), Ct))
            .Succeeded.Should()
            .BeFalse();

        Balance(CREDITS_TYPE_ID).Should().Be(100);
        Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task Debit_OfTwoCurrencies_TheSecondShort_TakesNeitherAndWritesNone()
    {
        Seed(credits: 100, diamonds: 10);
        var wallet = await NewWalletAsync();

        var result = await wallet.TryDebitAsync(
            [Debit(CurrencyKind.Credits, 30), Debit(Diamonds, 11)],
            Cause(),
            Ct
        );

        result.Succeeded.Should().BeFalse();
        result.Failure!.CurrencyKind.Should().Be(Diamonds);
        (Balance(CREDITS_TYPE_ID), Balance(DIAMONDS_TYPE_ID)).Should().Be((100, 10));
        Movements().Should().BeEmpty();
        (await wallet.GetAmountForCurrencyAsync(CurrencyKind.Credits, Ct)).Should().Be(100);
    }

    [Theory]
    [InlineData("credit", true)]
    [InlineData("credit", false)]
    [InlineData("reward", true)]
    [InlineData("reward", false)]
    [InlineData("referenced", true)]
    [InlineData("referenced", false)]
    [InlineData("achievement", true)]
    [InlineData("achievement", false)]
    public async Task Credit_WritesOneMovementWithTheBalance(string method, bool held)
    {
        if (held)
            Seed(credits: 100);
        var wallet = await NewWalletAsync();
        var cause = MovementCause.New(MovementCauses.REWARD_QUEST, PLAYER_ID, 9);

        (await CreditAsync(wallet, method, 25, cause)).Should().BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(held ? 125 : 25);
        var row = Movements().Should().ContainSingle().Subject;
        (row.OperationId, row.Seq, row.Cause, row.CauseRef, row.Actor)
            .Should()
            .Be((cause.OperationId, (short)0, MovementCauses.REWARD_QUEST, "9", PLAYER_ID));
        (row.AssetKind, row.CurrencyType, row.Amount)
            .Should()
            .Be((AssetKind.Currency, CurrencyType.Credits, 25L));
        (row.FromOwner, row.FromKind).Should().Be((0, PlaceKind.None));
        (row.ToOwner, row.ToKind).Should().Be((PLAYER_ID, PlaceKind.Wallet));
    }

    [Theory]
    [InlineData("referenced")]
    [InlineData("achievement")]
    public async Task Credit_Replayed_WritesNoSecondMovement(string method)
    {
        var wallet = await NewWalletAsync();

        (await CreditAsync(wallet, method, 25, Cause())).Should().BeTrue();
        (await CreditAsync(wallet, method, 25, Cause())).Should().BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(25);
        Movements().Should().ContainSingle();
    }

    [Fact]
    public async Task Credit_Refused_WritesNone()
    {
        var wallet = await NewWalletAsync();

        (await wallet.CreditAsync(CurrencyKind.Credits, 0, Cause(), Ct)).Should().BeFalse();
        (await wallet.CreditAsync(CurrencyKind.Emeralds, 5, Cause(), Ct)).Should().BeFalse();

        Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task RecordingOff_WritesNoRows_AndTheBalancesMoveAsBefore()
    {
        _config.Set(new() { Enabled = false });
        Seed(credits: 100, diamonds: 10);
        var wallet = await NewWalletAsync();

        (await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 30)], Cause(), Ct))
            .Succeeded.Should()
            .BeTrue();
        (
            await wallet.TryDebitAsync(
                [Debit(CurrencyKind.Credits, 10), Debit(Diamonds, 4)],
                Cause(),
                Ct
            )
        )
            .Succeeded.Should()
            .BeTrue();
        foreach (var method in new[] { "credit", "reward", "referenced", "achievement" })
            (await CreditAsync(wallet, method, 5, Cause())).Should().BeTrue();

        (Balance(CREDITS_TYPE_ID), Balance(DIAMONDS_TYPE_ID)).Should().Be((80, 6));
        Outbox().Should().BeEmpty();
    }

    [Theory]
    [InlineData("credit", true)]
    [InlineData("credit", false)]
    [InlineData("reward", true)]
    [InlineData("reward", false)]
    public async Task Credit_RepeatedUnderTheSameOperation_PaysOnce(string method, bool recording)
    {
        _config.Set(new() { Enabled = recording });
        Seed(credits: 100);
        var wallet = await NewWalletAsync();
        var cause = Cause();

        (await CreditAsync(wallet, method, 25, cause)).Should().BeTrue();
        (await CreditAsync(wallet, method, 25, cause)).Should().BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(125);
        (await wallet.GetAmountForCurrencyAsync(CurrencyKind.Credits, Ct)).Should().Be(125);
        Movements().Should().HaveCount(recording ? 1 : 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Debit_RepeatedUnderTheSameOperation_ChargesOnce(bool recording)
    {
        _config.Set(new() { Enabled = recording });
        Seed(credits: 100);
        var wallet = await NewWalletAsync();
        var cause = Cause();

        for (var i = 0; i < 2; i++)
            (await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 30)], cause, Ct))
                .Succeeded.Should()
                .BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(70);
        (await wallet.GetAmountForCurrencyAsync(CurrencyKind.Credits, Ct)).Should().Be(70);
        Movements().Should().HaveCount(recording ? 1 : 0);
    }

    [Fact]
    public async Task Debit_Refused_KeepsNoReceipt_SoTheSameOperationCanStillGoThrough()
    {
        Seed(credits: 20);
        var wallet = await NewWalletAsync();
        var cause = Cause();

        (await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 30)], cause, Ct))
            .Succeeded.Should()
            .BeFalse();
        (await wallet.CreditAsync(CurrencyKind.Credits, 10, Cause(), Ct)).Should().BeTrue();
        (await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 30)], cause, Ct))
            .Succeeded.Should()
            .BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(0);
    }

    [Fact]
    public async Task ReferencedDebit_Retried_UnderANewCause_TakesOnce_AndSaysSo()
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();

        var first = await wallet.TryDebitAsync(
            [Debit(CurrencyKind.Credits, 30)],
            "ledger:take-1",
            Cause(),
            Ct
        );
        var retry = await wallet.TryDebitAsync(
            [Debit(CurrencyKind.Credits, 30)],
            "ledger:take-1",
            Cause(),
            Ct
        );

        (first.Succeeded, first.AlreadyApplied).Should().Be((true, false));
        (retry.Succeeded, retry.AlreadyApplied).Should().Be((true, true));
        Balance(CREDITS_TYPE_ID).Should().Be(70);
        Movements().Should().ContainSingle();
    }

    [Fact]
    public async Task ReferencedDebit_Refused_KeepsNoReceipt_SoTheReferenceCanStillGoThrough()
    {
        Seed(credits: 20);
        var wallet = await NewWalletAsync();

        (
            await wallet.TryDebitAsync(
                [Debit(CurrencyKind.Credits, 30)],
                "ledger:take-1",
                Cause(),
                Ct
            )
        )
            .Succeeded.Should()
            .BeFalse();
        (await wallet.CreditAsync(CurrencyKind.Credits, 10, Cause(), Ct)).Should().BeTrue();
        var later = await wallet.TryDebitAsync(
            [Debit(CurrencyKind.Credits, 30)],
            "ledger:take-1",
            Cause(),
            Ct
        );

        (later.Succeeded, later.AlreadyApplied).Should().Be((true, false));
        Balance(CREDITS_TYPE_ID).Should().Be(0);
    }

    [Fact]
    public async Task ReferencedDebit_UnderTheReferenceOfAnEarlierCredit_TakesTheMoney_ThenOnce()
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();

        (await wallet.CreditAsync(CurrencyKind.Credits, 30, "ledger:grant-1", Cause(), Ct))
            .Should()
            .Be(WalletCreditResult.Applied);
        var takeBack = await wallet.TryDebitAsync(
            [Debit(CurrencyKind.Credits, 30)],
            "ledger:grant-1",
            Cause(),
            Ct
        );
        var again = await wallet.TryDebitAsync(
            [Debit(CurrencyKind.Credits, 30)],
            "ledger:grant-1",
            Cause(),
            Ct
        );

        (takeBack.Succeeded, takeBack.AlreadyApplied).Should().Be((true, false));
        (again.Succeeded, again.AlreadyApplied).Should().Be((true, true));
        Balance(CREDITS_TYPE_ID).Should().Be(100);
    }

    [Fact]
    public async Task ACreditAndADebit_UnderOneCause_AreTwoChanges_NotARepeat()
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();
        var cause = Cause();

        (await wallet.CreditAsync(CurrencyKind.Credits, 10, cause, Ct)).Should().BeTrue();
        var debit = await wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 10)], cause, Ct);

        (debit.Succeeded, debit.AlreadyApplied).Should().Be((true, false));
        Balance(CREDITS_TYPE_ID).Should().Be(100);
    }

    [Theory]
    [InlineData("#op:0:0199f0aa000070008000000000000003")]
    [InlineData("#debit:ledger:grant-1")]
    [InlineData("#DEBIT:ledger:grant-1")]
    public async Task AReferenceInTheWalletsOwnReceiptKeys_IsRefused_BothWays(string reference)
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();

        await FluentActions
            .Awaiting(() => wallet.CreditAsync(CurrencyKind.Credits, 10, reference, Cause(), Ct))
            .Should()
            .ThrowAsync<ArgumentException>();
        await FluentActions
            .Awaiting(() =>
                wallet.TryDebitAsync([Debit(CurrencyKind.Credits, 10)], reference, Cause(), Ct)
            )
            .Should()
            .ThrowAsync<ArgumentException>();
        Balance(CREDITS_TYPE_ID).Should().Be(100);
    }

    [Fact]
    public async Task Reversal_TakingBackCredits_AskedTwice_TakesThemOnce_AsAReversalMovement()
    {
        Seed(credits: 100);
        var factory = WalletFactory(await NewWalletAsync());
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        var service = new Turbo.Operations.Movements.AssetReversalService(
            factory,
            _db,
            NullLogger<Turbo.Operations.Movements.AssetReversalService>.Instance
        );
        var request = new ReversalRequest
        {
            OperationId = "rev-1",
            ReversedOperationId = "0199f0aa000070008000000000000002",
            ActorPlayerId = 9,
            Reason = "credits paid twice",
            Asset = new AssetRef(AssetKind.Currency, 0, 0, CurrencyKind.Credits),
            Expected = new AssetPlace(PLAYER_ID, PlaceKind.Wallet, 0, null),
            Target = AssetPlace.Nowhere,
            Amount = 40,
        };

        var first = await service.MoveAsync(request, Ct);
        var again = await service.MoveAsync(request, Ct);

        first.Outcome.Should().Be(ReversalOutcome.Applied);
        again.Outcome.Should().Be(ReversalOutcome.AlreadyApplied);
        Balance(CREDITS_TYPE_ID).Should().Be(60);
        var row = Movements().Should().ContainSingle().Subject;
        (row.Cause, row.CauseRef, row.OperationId, row.Actor, row.Amount)
            .Should()
            .Be((MovementCauses.REVERSAL, request.ReversedOperationId, "rev-1", 9, 40L));
        (row.FromOwner, row.FromKind, row.ToKind)
            .Should()
            .Be((PLAYER_ID, PlaceKind.Wallet, PlaceKind.None));
    }

    [Fact]
    public async Task Reversal_TakingBackMoreThanTheBalance_IsNotWhereExpected_AndTakesNothing()
    {
        Seed(credits: 10);
        var factory = WalletFactory(await NewWalletAsync());
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        var service = new Turbo.Operations.Movements.AssetReversalService(
            factory,
            _db,
            NullLogger<Turbo.Operations.Movements.AssetReversalService>.Instance
        );

        var result = await service.MoveAsync(
            new ReversalRequest
            {
                OperationId = "rev-1",
                ReversedOperationId = "0199f0aa000070008000000000000002",
                ActorPlayerId = 9,
                Reason = "credits paid twice",
                Asset = new AssetRef(AssetKind.Currency, 0, 0, CurrencyKind.Credits),
                Expected = new AssetPlace(PLAYER_ID, PlaceKind.Wallet, 0, null),
                Target = AssetPlace.Nowhere,
                Amount = 40,
            },
            Ct
        );

        result.Outcome.Should().Be(ReversalOutcome.NotWhereExpected);
        Balance(CREDITS_TYPE_ID).Should().Be(10);
        Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task Credits_UnderOneOperationAtDifferentSeqs_AreEachApplied()
    {
        Seed(credits: 100);
        var wallet = await NewWalletAsync();
        var cause = Cause();

        (await wallet.CreditAsync(CurrencyKind.Credits, 5, cause, Ct)).Should().BeTrue();
        (await wallet.CreditAsync(CurrencyKind.Credits, 5, cause.StartingAt(1), Ct))
            .Should()
            .BeTrue();

        Balance(CREDITS_TYPE_ID).Should().Be(110);
    }

    [Fact]
    public async Task Refund_RepeatedForTheSameCharge_PaysOnce_EveryCurrencyAtItsOwnSeq()
    {
        Seed(credits: 100, diamonds: 10);
        var wallet = await NewWalletAsync();
        var factory = WalletFactory(wallet);
        var charge = MovementCause.New(MovementCauses.GROUP_CREATE, PLAYER_ID, 12);
        WalletDebit[] debits = [Debit(CurrencyKind.Credits, 10), Debit(Diamonds, 2)];

        (await wallet.TryDebitAsync([.. debits.Cast<WalletDebitRequest>()], charge, Ct))
            .Succeeded.Should()
            .BeTrue();
        for (var i = 0; i < 2; i++)
            await factory.RefundAsync(
                (PlayerId)PLAYER_ID,
                debits,
                charge,
                NullLogger.Instance,
                "a test"
            );

        (Balance(CREDITS_TYPE_ID), Balance(DIAMONDS_TYPE_ID)).Should().Be((100, 10));
        var refunds = Movements().Where(x => x.CauseRef == charge.OperationId).ToList();
        refunds
            .Select(x => (x.Seq, x.CurrencyType, x.Amount))
            .Should()
            .Equal(
                ((short)0, CurrencyType.Credits, 10L),
                ((short)1, CurrencyType.ActivityPoints, 2L)
            );
    }

    [Fact]
    public async Task Refund_CreditsUnderTheChargesCause_AsAnOperationThatNamesIt()
    {
        var factory = _fakes.Create<IGrainFactory>();
        var charge = MovementCause.New(MovementCauses.GROUP_CREATE, PLAYER_ID, 12);

        await factory.RefundAsync(
            (PlayerId)PLAYER_ID,
            [Debit(CurrencyKind.Credits, 10), Debit(Diamonds, 2)],
            charge,
            NullLogger.Instance,
            "a test"
        );

        var refunds = _fakes
            .Log.Of(nameof(IPlayerWalletGrain.CreditAsync))
            .Select(x => (MovementCause)x.Args[2]!)
            .ToList();
        refunds.Should().HaveCount(2);
        refunds.Should().AllSatisfy(x => x.OperationId.Should().Be(refunds[0].OperationId));
        refunds.Select(x => x.FirstSeq).Should().Equal(0, 1);
        refunds[0].OperationId.Should().Be(charge.ForRefund().OperationId);
        refunds[0].Cause.Should().Be(MovementCauses.GROUP_CREATE);
        refunds[0].ActorPlayerId.Should().Be(PLAYER_ID);
        refunds[0].CauseRef.Should().Be(charge.OperationId);
        refunds[0].OperationId.Should().NotBe(charge.OperationId);
    }

    private static Task<bool> CreditAsync(
        IPlayerWalletGrain wallet,
        string method,
        int amount,
        MovementCause cause
    ) =>
        method switch
        {
            "credit" => wallet.CreditAsync(CurrencyKind.Credits, amount, cause, Ct),
            "reward" => wallet.CreditRewardAsync(CurrencyKind.Credits, amount, cause, Ct),
            "referenced" => Applied(
                wallet.CreditAsync(CurrencyKind.Credits, amount, "test:1", cause, Ct)
            ),
            "achievement" => wallet.CreditAchievementAsync(
                $"award-{amount}",
                CurrencyKind.Credits,
                amount,
                cause,
                Ct
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

    private static async Task<bool> Applied(Task<WalletCreditResult> credit) =>
        await credit is WalletCreditResult.Applied or WalletCreditResult.AlreadyApplied;

    private static MovementCause Cause() => MovementCause.New("test", PLAYER_ID);

    // A grain factory whose wallet for the player is the real one.
    private IGrainFactory WalletFactory(IPlayerWalletGrain wallet)
    {
        _fakes.Instances[(typeof(IPlayerWalletGrain), (long)PLAYER_ID)] = wallet;

        return _fakes.Create<IGrainFactory>();
    }

    private static WalletDebitRequest Debit(CurrencyKind kind, int amount) =>
        new() { CurrencyKind = kind, Amount = amount };

    private void Seed(int credits, int? diamonds = null)
    {
        _db.Insert(
            new PlayerCurrencyEntity
            {
                Id = CREDITS_TYPE_ID,
                PlayerEntityId = PLAYER_ID,
                CurrencyTypeEntityId = CREDITS_TYPE_ID,
                Amount = credits,
            }
        );
        if (diamonds is { } amount)
            _db.Insert(
                new PlayerCurrencyEntity
                {
                    Id = DIAMONDS_TYPE_ID,
                    PlayerEntityId = PLAYER_ID,
                    CurrencyTypeEntityId = DIAMONDS_TYPE_ID,
                    Amount = amount,
                }
            );
    }

    private int Balance(int typeId)
    {
        using var db = _db.CreateDbContext();

        return db
            .PlayerCurrencies.Where(x =>
                x.PlayerEntityId == PLAYER_ID && x.CurrencyTypeEntityId == typeId
            )
            .Select(x => x.Amount)
            .Single();
    }

    private List<AssetMovementOutboxEntity> Outbox()
    {
        using var db = _db.CreateDbContext();

        return db.AssetMovementOutbox.AsNoTracking().OrderBy(x => x.Id).ToList();
    }

    private List<AssetMovementOutboxEntity> Movements() =>
        Outbox()
            .Where(x =>
                x.Cause
                    is not (MovementCauses.RECORDING_STARTED or MovementCauses.RECORDING_STOPPED)
            )
            .ToList();

    private async Task<IPlayerWalletGrain> NewWalletAsync()
    {
        var recorder = new AssetMovementRecorder(
            _config,
            _db,
            NullLogger<AssetMovementRecorder>.Instance
        );
        await recorder.StartAsync(Ct);

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
        RoomHarness.SetField(grain, "_movements", recorder);
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerWalletGrain)grain;
    }
}
