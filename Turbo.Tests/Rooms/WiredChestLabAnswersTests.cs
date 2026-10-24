using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The wired chest boxes as Habbo answered them (official client capture 2026-10-24, build
/// WIN63-202610201032, one account without the trading pass in its own room; wired matrix W7,
/// case ids in each test). A box that cannot start tells only the room (the <c>wired.error</c>
/// bubble); locked chests answer 15; a user who may not trade answers 8 before anything about
/// the chests' contents, and only a contract transaction that started fires Transaction Failed.
/// The chest conditions fold each chest, or each item type, with the furni quantifier.
/// </summary>
public sealed class WiredChestLabAnswersTests
{
    private const int USER = 5;
    private const int BOX = 2;
    private const int CHEST = 30;
    private const int CONTRACT = 31;
    private const int LAMP = 32;
    private const int CHEST_COINS = 500;
    private const string TRANSACTION_FAILURE = "${room.error.wired.transaction_failure}";

    private const int LESS = 0;
    private const int EQUAL = 1;
    private const int GREATER = 2;
    private const int GREATER_OR_EQUAL = 5;
    private const int ALL = 0;
    private const int ANY = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(8, 8);
    private readonly List<int?> _withdrawn = [];

    // condition.45.R1: no chest picked, 8 of 8. condition.45.R1b: one picked empty chest, 8 of 8.
    [Theory]
    [InlineData(false, ALL, GREATER, true)]
    [InlineData(false, ALL, EQUAL, true)]
    [InlineData(false, ANY, GREATER, false)]
    [InlineData(false, ANY, EQUAL, false)]
    [InlineData(true, ALL, EQUAL, true)]
    [InlineData(true, ALL, GREATER, false)]
    [InlineData(true, ALL, LESS, false)]
    [InlineData(true, ALL, GREATER_OR_EQUAL, true)]
    public async Task Chest_has_amount_folds_the_picked_chests_with_its_quantifier(
        bool chestPicked,
        int quantifier,
        int comparison,
        bool passes
    )
    {
        AddChest(locked: false, coins: 0);
        var condition = _room.AddBox<WiredConditionChestHasItems>(
            BOX,
            0,
            0,
            "wf_cnd_chest_has_items"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [0, 0, -20, comparison],
                stuffIds: chestPicked ? [CHEST] : [],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [quantifier],
                variableIds: ["n"]
            )
        ).Should().BeTrue();

        condition.GetQuantifierType().Should().Be((byte)WiredQuantifierType.Furni);
        condition.Evaluate(Context()).Should().Be(passes);
    }

    // condition.46.R1 (a lamp type, an empty furni chest), R1b (no type picked), R1c (no chest
    // picked), 2 of 2 each, quantifier All.
    [Theory]
    [InlineData(true, true, EQUAL, true)]
    [InlineData(true, true, GREATER, false)]
    [InlineData(false, true, GREATER, true)]
    [InlineData(true, false, GREATER, false)]
    public async Task Chest_has_item_types_folds_the_types_counted_over_all_chests(
        bool typePicked,
        bool chestPicked,
        int comparison,
        bool passes
    )
    {
        _room.AddFloorItem(LAMP, 3, 3);
        _room.AddFloorItem(
            CHEST,
            4,
            4,
            "wired_chest_furni",
            createLogic: (factory, ctx) => new FurnitureWiredFurniChestLogic(factory, ctx)
        );
        var condition = _room.AddBox<WiredConditionChestHasItemTypes>(
            BOX,
            0,
            0,
            "wf_cnd_chest_has_item_type"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [0, 0, -20, comparison],
                stuffIds: typePicked ? [LAMP] : [],
                stuffIds2: chestPicked ? [CHEST] : [],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                    [WiredFurniSourceType.TriggeredItem],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [ALL],
                variableIds: ["n"]
            )
        ).Should().BeTrue();

        condition.Evaluate(Context()).Should().Be(passes);
    }

    // action.45.R1b (no chest picked, bubbles only >= ~21 s apart) and action.45.R1 (the user
    // selection empty): no WiredTransactionFail, one bubble for two fires 2.4 s apart.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Give_currency_with_nothing_to_give_from_or_to_tells_only_the_room(
        bool chestPicked,
        bool userSelected
    )
    {
        AddChest(locked: false, coins: CHEST_COINS);
        var box = await AddGiveCurrencyAsync(chestPicked ? [CHEST] : [], mode: 0);
        var ctx = ExecutionContext(userSelected);

        await box.ExecuteAsync(ctx, Ct);
        _room.Harness.Clock.AdvanceMs(2400);
        await box.ExecuteAsync(ctx, Ct);

        _room.Harness.Fakes.Sent<WiredTransactionFailMessageComposer>().Should().BeEmpty();
        Notices().Should().Equal(TRANSACTION_FAILURE);
        _withdrawn.Should().BeEmpty();
    }

    // action.45.B1: a locked credit chest answers 15 on every fire, the bubble on the first.
    [Fact]
    public async Task Give_currency_from_a_locked_chest_tells_the_user_15_every_time()
    {
        AddChest(locked: true, coins: CHEST_COINS);
        var box = await AddGiveCurrencyAsync([CHEST], mode: 0);
        var ctx = ExecutionContext(userSelected: true);

        await box.ExecuteAsync(ctx, Ct);
        _room.Harness.Clock.AdvanceMs(2400);
        await box.ExecuteAsync(ctx, Ct);

        Failures()
            .Should()
            .Equal(
                WiredTransactionFailureType.NoOrLockedChests,
                WiredTransactionFailureType.NoOrLockedChests
            );
        Notices().Should().Equal(TRANSACTION_FAILURE);
    }

    // action.45.B1b (an amount and everything, 4 of 4): the user may not trade, so 8 comes before
    // anything about what the chest holds, and no bubble.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Give_currency_to_a_user_who_may_not_trade_tells_them_8(int mode)
    {
        AddChest(locked: false, coins: CHEST_COINS);
        var box = await AddGiveCurrencyAsync([CHEST], mode);
        _room.Harness.Fakes.Handlers["MayTradeAsync"] = _ => Task.FromResult(false);

        (await box.ExecuteAsync(ExecutionContext(userSelected: true), Ct)).Should().BeFalse();

        Failures().Should().Equal(WiredTransactionFailureType.UserCantTrade);
        Notices().Should().BeEmpty();
        _withdrawn.Should().BeEmpty();
    }

    [Fact]
    public async Task With_the_trade_rule_off_the_chest_gives_to_a_user_who_may_not_trade()
    {
        _room.Harness.Room.UseWiredChestConfig(
            new WiredChestConfig { RefuseWhoMayNotTrade = false }
        );
        AddChest(locked: false, coins: CHEST_COINS);
        var box = await AddGiveCurrencyAsync([CHEST], mode: 0);
        _room.Harness.Fakes.Handlers["MayTradeAsync"] = _ => Task.FromResult(false);

        (await box.ExecuteAsync(ExecutionContext(userSelected: true), Ct)).Should().BeTrue();

        _withdrawn.Should().Equal(1);
    }

    // action.47.B1 (4 of 4): a credit chest and a fresh payment contract, the user may not trade:
    // 8 to the user and Transaction Failed for that user with reason 8; no trade window.
    [Fact]
    public async Task A_started_contract_transaction_fails_with_8_and_fires_transaction_failed()
    {
        AddChest(locked: false, coins: 0);
        _room.AddFloorItem(
            CONTRACT,
            5,
            5,
            "wired_contract_payment",
            createLogic: (factory, ctx) => new FurnitureWiredPaymentContractLogic(factory, ctx)
        );
        var box = await AddInitiateTransactionAsync([CHEST], [CONTRACT]);
        _room.Harness.Fakes.Handlers["MayTradeAsync"] = _ => Task.FromResult(false);
        var heard = Listen();

        await box.ExecuteAsync(ExecutionContext(userSelected: true), Ct);

        Failures().Should().Equal(WiredTransactionFailureType.UserCantTrade);
        var failed = heard.OfType<WiredTransactionFailedEvent>().Single();
        failed.Reason.Should().Be(WiredTransactionFailureType.UserCantTrade);
        failed.PlayerId.Should().Be(_room.Avatars[USER].PlayerId);
        failed.SourceId.Value.Should().Be(CONTRACT);
        _room.Harness.Fakes.Log.Of(nameof(IWiredTradeGrain.StartContractAsync)).Should().BeEmpty();
        Notices().Should().BeEmpty();
    }

    // action.47.R1 (nothing picked), R1b (a contract, no chest), B3 (a chest and a fresh reward
    // contract, which gives nothing): no WiredTransactionFail, no Transaction Failed, the bubble.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task An_initiate_transaction_that_cannot_start_tells_only_the_room(
        bool chestPicked,
        bool contractPicked
    )
    {
        AddChest(locked: false, coins: CHEST_COINS);
        _room.AddFloorItem(
            CONTRACT,
            5,
            5,
            "wired_contract_reward",
            createLogic: (factory, ctx) => new FurnitureWiredRewardContractLogic(factory, ctx)
        );
        var box = await AddInitiateTransactionAsync(
            chestPicked ? [CHEST] : [],
            contractPicked ? [CONTRACT] : []
        );
        var heard = Listen();

        await box.ExecuteAsync(ExecutionContext(userSelected: true), Ct);

        _room.Harness.Fakes.Sent<WiredTransactionFailMessageComposer>().Should().BeEmpty();
        heard.OfType<WiredTransactionFailedEvent>().Should().BeEmpty();
        Notices().Should().Equal(TRANSACTION_FAILURE);
    }

    // addon.20.E9 (1 of 1): the fresh defaults, both sides off, are refused.
    [Fact]
    public async Task A_custom_contract_with_both_sides_off_is_refused_as_misconfigured()
    {
        _room.AddBox<WiredAddonCustomContract>(BOX, 0, 0, "wf_xtra_custom_contract");

        var result = await _room.SaveWithResultAsync<UpdateAddonMessage>(
            BOX,
            intParams: [0, 0, 0, 1, -20, 0, 0, 0, 1, -20],
            variableIds: ["n", "n"]
        );

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be("wiredfurni.error.misconfigured_contract");
    }

    private void AddChest(bool locked, int coins)
    {
        var chest = (FurnitureWiredChestLogic)
            _room
                .AddFloorItem(
                    CHEST,
                    4,
                    4,
                    "wired_chest_coins",
                    createLogic: (factory, ctx) => new FurnitureWiredCoinsChestLogic(factory, ctx)
                )
                .Logic;

        chest
            .SetMapDataAsync(
                new Dictionary<string, string>
                {
                    [WiredChestData.IS_WIRED_ENABLED] = WiredChestData.TRUE,
                    [WiredChestData.LOCKED] = locked ? WiredChestData.TRUE : WiredChestData.FALSE,
                },
                refresh: false
            )
            .GetAwaiter()
            .GetResult();
        // Summary's setter is private to the chest base class.
        typeof(FurnitureWiredChestLogic)
            .GetProperty(nameof(FurnitureWiredChestLogic.Summary))!
            .SetValue(chest, WiredChestSummarySnapshot.Empty with { Coins = coins });
        _room.Harness.Fakes.Handlers[nameof(IWiredChestGrain.WithdrawAsync)] = call =>
        {
            var amount = ((WiredChestWithdrawRequest)call.Args[0]!).Amount;

            _withdrawn.Add(amount);

            return Task.FromResult(
                new WiredChestMoveResultSnapshot
                {
                    Failure = null,
                    TransactionId = 1,
                    Coins = amount ?? coins,
                    Items = [],
                    Summary = WiredChestSummarySnapshot.Empty,
                }
            );
        };
    }

    private async Task<WiredActionGiveCurrencyFromChest> AddGiveCurrencyAsync(
        int[] chests,
        int mode
    )
    {
        var box = _room.AddBox<WiredActionGiveCurrencyFromChest>(BOX, 0, 0, "wf_act_give_currency");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [mode, 1, 0, -20, 1, 11],
                stuffIds: chests,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0],
                variableIds: ["n"]
            )
        ).Should().BeTrue();

        return box;
    }

    private async Task<WiredActionInitiateTransaction> AddInitiateTransactionAsync(
        int[] chests,
        int[] contracts
    )
    {
        var box = _room.AddBox<WiredActionInitiateTransaction>(
            BOX,
            0,
            0,
            "wf_act_init_transaction"
        );

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0, 10, 0, -20, 0, 300],
                stuffIds: chests,
                stuffIds2: contracts,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    private WiredExecutionContext ExecutionContext(bool userSelected)
    {
        _room.Enter(USER, 1, 1);

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        if (userSelected)
            ctx.Selected.SelectedAvatarIds.Add(USER);

        return ctx;
    }

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

    private List<RoomEvent> Listen()
    {
        var heard = new HeardEvents();

        _room.Harness.Module<RoomEventModule>().Register(heard);

        return heard.Events;
    }

    private IEnumerable<WiredTransactionFailureType> Failures() =>
        _room.Harness.Fakes.Sent<WiredTransactionFailMessageComposer>().Select(x => x.FailureType);

    private IEnumerable<string> Notices() =>
        _room
            .Harness.Fakes.Sent<NotificationDialogMessageComposer>()
            .Select(x => x.Parameters["message"]);

    private sealed class HeardEvents : IRoomEventListener
    {
        public List<RoomEvent> Events { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            Events.Add(evt);

            return Task.CompletedTask;
        }
    }
}
