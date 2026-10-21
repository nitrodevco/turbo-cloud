using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Orleans.Streams;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.WiredTrading;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired contracts: an offer is held to the contract's options the way the client counts it,
/// a contract keeps only what its editor allows, and a contract's trade window is opened once,
/// replaced only when wired cancelled it, and carried out only when its requirements are met.
/// </summary>
public class WiredContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int CHAIR_SPRITE = 30;
    private const int LAMP_SPRITE = 31;
    private const int CONTRACT = 7;
    private const int PLAYER = 5;

    private static readonly ChestItemTypeSnapshot Chair = Type(CHAIR_SPRITE);
    private static readonly ChestItemTypeSnapshot Lamp = Type(LAMP_SPRITE);

    [Fact]
    public void An_offer_meets_the_option_it_covers_most_often()
    {
        // Two chairs, or one lamp and 10 credits.
        var request = Request(
            [Rule(Furni(Chair, 2)), Rule(Furni(Lamp, 1), Coins(10))],
            TradeRequirementRulesType.AutoMultiplier,
            multiplier: 5
        );
        List<FurnitureItemSnapshot> offer =
        [
            Item(1, CHAIR_SPRITE),
            Item(2, LAMP_SPRITE),
            Item(3, LAMP_SPRITE),
            Item(4, 0, "CF_10_goldbar"),
            Item(5, 0, "CF_10_goldbar"),
        ];

        WiredContractOffers.Evaluate(request, offer).Should().Be((1, 2));
        WiredContractOffers
            .Take(request, offer, 1, 2)
            .Select(x => x.ItemId.Value)
            .Should()
            .BeEquivalentTo([2, 3, 4, 5]);
    }

    [Fact]
    public void Only_what_some_option_asks_for_may_be_offered()
    {
        var request = Request([Rule(Furni(Chair, 1))], TradeRequirementRulesType.Single, 1);

        WiredContractOffers.CanOffer(request, Item(1, CHAIR_SPRITE)).Should().BeTrue();
        WiredContractOffers.CanOffer(request, Item(2, LAMP_SPRITE)).Should().BeFalse();
        WiredContractOffers.CanOffer(request, Item(3, 0, "CF_10_goldbar")).Should().BeFalse();
    }

    [Fact]
    public void A_multiplier_is_accepted_only_when_met_as_often_as_it_asks()
    {
        var request = Request([Rule(Furni(Chair, 1))], TradeRequirementRulesType.Multiplier, 3);

        WiredContractOffers.CanAccept(request, 2).Should().BeFalse();
        WiredContractOffers.CanAccept(request, 3).Should().BeTrue();
        WiredContractOffers.TimesToCarryOut(request, 7).Should().Be(3);
    }

    [Fact]
    public void A_new_reward_contract_shows_its_popup_by_default()
    {
        // Habbo's editor on a fresh reward contract: "Show pop-up by default" ticked.
        CreateRoomWithContract(reward: true).Contract.Contract.ShowDialog.Should().BeTrue();
        CreateRoomWithContract().Contract.Contract.ShowDialog.Should().BeFalse();
    }

    [Fact]
    public async Task A_contract_with_more_options_than_the_editor_allows_is_not_saved()
    {
        var (room, contract) = CreateRoomWithContract();
        var tooMany = Contract([.. Enumerable.Repeat(Rule(Coins(1)), 4)]);

        var saved = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(1, 1),
            CONTRACT,
            new UpdateContractInteraction { Contract = tooMany },
            Ct
        );

        saved.Should().BeFalse();
        contract.Contract.Definition.YouGive!.Value.Should().BeEmpty();
        room.Fakes.Sent<WiredContractUpdateResultMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => !x.IsSuccess && x.FailCode == WiredContractFailCodes.INVALID_RULES);
    }

    [Fact]
    public async Task A_valid_contract_is_kept_as_saved()
    {
        var (room, contract) = CreateRoomWithContract();

        var saved = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(1, 1),
            CONTRACT,
            new UpdateContractInteraction { Contract = Contract([Rule(Coins(25))]) },
            Ct
        );

        saved.Should().BeTrue();
        contract.Contract.Definition.YouGive!.Value.Single().Nodes.Single().Amount.Should().Be(25);
    }

    [Fact]
    public async Task A_contract_trade_is_carried_out_only_once_its_requirements_are_met()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var offered = new Queue<int>([1, 2]);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([
                Item(offered.Dequeue(), CHAIR_SPRITE),
            ]);
        fakes.Handlers["CompleteWiredContractTradeAsync"] = _ =>
            Task.FromResult<WiredTransactionFailureType?>(null);

        await trade.StartContractAsync(
            Request([Rule(Furni(Chair, 2))], TradeRequirementRulesType.Single, 1),
            Ct
        );
        await trade.AddItemsAsync([1], Ct);

        // One chair of two: the trade cannot be accepted yet.
        await trade.ConfirmAsync(false, Ct);
        await trade.ConfirmAsync(true, Ct);
        fakes.Log.Of("CompleteWiredContractTradeAsync").Should().BeEmpty();
        fakes
            .Sent<WiredTradeItemsUpdateMessageComposer>(SendRoute.Direct)
            .Last()
            .CanAccept.Should()
            .BeFalse();

        await trade.AddItemsAsync([2], Ct);
        await trade.ConfirmAsync(false, Ct);
        await trade.ConfirmAsync(true, Ct);

        var completed = fakes.Log.Of("CompleteWiredContractTradeAsync").Single();
        ((ImmutableArray<FurnitureItemSnapshot>)completed.Args[2]!)
            .Select(x => x.ItemId.Value)
            .Should()
            .BeEquivalentTo([1, 2]);
        fakes
            .Sent<WiredTransactionSuccessMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.Contents.Type == WiredTransactionSuccessType.Payment);
    }

    [Fact]
    public async Task A_final_confirm_before_accepting_cancels_the_trade_as_invalid()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([Item(1, CHAIR_SPRITE)]);
        fakes.Handlers["CompleteWiredContractTradeAsync"] = _ =>
            Task.FromResult<WiredTransactionFailureType?>(null);

        await trade.StartContractAsync(
            Request([Rule(Furni(Chair, 1))], TradeRequirementRulesType.Single, 1),
            Ct
        );
        await trade.AddItemsAsync([1], Ct);

        await trade.ConfirmAsync(true, Ct);

        fakes
            .Sent<WiredTradeCancelledMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.FailureType == WiredTransactionFailureType.Invalid);
        fakes
            .Log.Of("ReportWiredTransactionFailedAsync")
            .Should()
            .ContainSingle(x =>
                (WiredTransactionFailureType)x.Args[2]! == WiredTransactionFailureType.Invalid
            );

        // The trade is over: accepting and confirming now moves nothing.
        await trade.ConfirmAsync(false, Ct);
        await trade.ConfirmAsync(true, Ct);
        fakes.Log.Of("CompleteWiredContractTradeAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task The_room_hears_where_a_contract_trade_is_until_it_ends()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var offered = new Queue<int>([1, 2]);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([
                Item(offered.Dequeue(), CHAIR_SPRITE),
            ]);
        fakes.Handlers["CompleteWiredContractTradeAsync"] = _ =>
            Task.FromResult<WiredTransactionFailureType?>(null);

        await trade.StartContractAsync(
            Request([Rule(Furni(Chair, 2))], TradeRequirementRulesType.Single, 1),
            Ct
        );
        await trade.AddItemsAsync([1], Ct);
        await trade.AddItemsAsync([2], Ct);
        await trade.ConfirmAsync(false, Ct);
        await trade.ConfirmAsync(true, Ct);

        var reported = fakes
            .Log.Of("ReportWiredTransactionProgressAsync")
            .Select(x => (WiredTransactionProgressSnapshot?)x.Args[1])
            .ToList();

        reported
            .Select(x =>
                x is null ? "ended" : $"{x.ContractId.Value} {x.State} {x.CanAccept} {x.Multiplier}"
            )
            .Should()
            .Equal(
                $"{CONTRACT} AddingItems False 0",
                $"{CONTRACT} AddingItems False 0",
                $"{CONTRACT} AddingItems True 1",
                $"{CONTRACT} Confirmed True 1",
                "ended"
            );
        reported[0]!.StartedAtMs.Should().BeGreaterThan(0);
        reported
            .Take(4)
            .Select(x => x!.StartedAtMs)
            .Distinct()
            .Should()
            .ContainSingle("the trade keeps the time it opened");
    }

    [Fact]
    public async Task A_second_contract_is_refused_while_the_player_is_trading()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var request = Request([Rule(Coins(10))], TradeRequirementRulesType.Single, 1);

        await trade.StartContractAsync(request, Ct);
        await trade.StartContractAsync(request, Ct);

        fakes.Sent<WiredTradeInitiateMessageComposer>(SendRoute.Direct).Should().ContainSingle();
        fakes
            .Log.Of("ReportWiredTransactionFailedAsync")
            .Should()
            .ContainSingle(x =>
                (WiredTransactionFailureType)x.Args[2]!
                == WiredTransactionFailureType.AlreadyTrading
            );
    }

    // The trade is memory only and nothing calls the grain while the window is open; Orleans
    // collects an idle activation while its player stays online, which forgot the trade and left
    // the window open on offers that went nowhere.
    [Fact]
    public async Task An_open_deposit_keeps_the_trade_grain_loaded_past_each_action()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([Item(1, CHAIR_SPRITE)]);

        await trade.StartChestDepositAsync(1, 100, WiredChestKind.Furni, Ct);
        await trade.AddItemsAsync([1], Ct);

        Pins(fakes)
            .Should()
            .Equal(
                // WiredChestConfig.TradeKeepAliveMinutes as shipped, from each action.
                [TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30)],
                "idle collection would forget the open trade"
            );
    }

    [Fact]
    public async Task A_contract_keeps_the_trade_grain_loaded_until_it_times_out()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var hours = (int)TimeSpan.FromHours(2).TotalSeconds;

        await trade.StartContractAsync(
            Request([Rule(Coins(10))], TradeRequirementRulesType.Single, 1) with
            {
                TimeoutSeconds = hours,
            },
            Ct
        );

        Pins(fakes)
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeGreaterThan(TimeSpan.FromSeconds(hours), "its own timer ends it");
    }

    private static IEnumerable<TimeSpan> Pins(Fakes fakes) =>
        fakes.Log.Of("DelayDeactivation").Select(call => (TimeSpan)call.Args[1]!);

    [Fact]
    public async Task A_trade_wired_cancelled_is_replaced_without_closing_the_window()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var request = Request([Rule(Coins(10))], TradeRequirementRulesType.Single, 1);

        await trade.StartContractAsync(request, Ct);
        await trade.CancelByWiredAsync([], Ct);
        await trade.StartContractAsync(request, Ct);

        fakes.Sent<WiredTradeCancelledMessageComposer>(SendRoute.Direct).Should().BeEmpty();
        fakes
            .Sent<WiredTradeInitiateMessageComposer>(SendRoute.Direct)
            .Select(x => x.OverridePreviousTrade)
            .Should()
            .Equal(false, true);
        fakes
            .Log.Of("ReportWiredTransactionFailedAsync")
            .Should()
            .ContainSingle(x =>
                (WiredTransactionFailureType)x.Args[2]!
                == WiredTransactionFailureType.TradeCancelled
            );
    }

    [Fact]
    public async Task A_reward_is_paid_out_of_the_chests_times_over_and_shown()
    {
        var (room, _) = CreateRoomWithContract();
        var chestItem = room.CreateFloorItem(
            100,
            3,
            3,
            Altitude.Zero,
            name: "wf_storage_coins1",
            logic: "wired_chest_coins",
            createLogic: (stuffDataFactory, ctx) =>
                new FurnitureWiredCoinsChestLogic(stuffDataFactory, ctx)
        );
        room.AddToRoom(chestItem);
        var chest = (FurnitureWiredChestLogic)chestItem.Logic;
        await chest.SetMapDataAsync(
            new Dictionary<string, string>
            {
                [WiredChestData.IS_WIRED_ENABLED] = WiredChestData.TRUE,
                [WiredChestData.LOCKED] = WiredChestData.FALSE,
            },
            refresh: false
        );
        typeof(FurnitureWiredChestLogic)
            .GetProperty(nameof(FurnitureWiredChestLogic.Summary))!
            .SetValue(chest, WiredChestSummarySnapshot.Empty with { Coins = 50 });
        room.Fakes.Handlers[nameof(IWiredChestGrain.WithdrawAsync)] = call =>
            Task.FromResult(
                new WiredChestMoveResultSnapshot
                {
                    Failure = null,
                    TransactionId = 1,
                    Coins = ((WiredChestWithdrawRequest)call.Args[0]!).Amount!.Value,
                    Items = [],
                    Summary = WiredChestSummarySnapshot.Empty,
                }
            );
        var player = room.Fakes.Create<Turbo.Primitives.Rooms.Object.Avatars.IRoomPlayer>(PLAYER);
        room.Fakes.Handlers["get_PlayerId"] = call =>
            call.Key is PLAYER ? (PlayerId)PLAYER : Fakes.NotHandled;
        room.Fakes.Handlers["get_Name"] = call => call.Key is PLAYER ? "player" : Fakes.NotHandled;
        var reward = new WiredContractSnapshot
        {
            ContractId = CONTRACT,
            Type = WiredContractType.Reward,
            Definition = new() { YouGive = [], YouGet = Rule(Coins(5)) },
            RewardText = "well done",
        };

        var heard = new HeardEvents();
        room.Module<RoomEventModule>().Register(heard);

        var failure = await room.Room.WiredTransactionSystem.RewardAsync(
            player,
            [chest],
            reward,
            CONTRACT,
            times: 2,
            Ct
        );

        failure.Should().BeNull();
        // What the @event.transaction_complete.* variables read: twice over, 10 credits out.
        heard
            .Events.OfType<WiredTransactionCompletedEvent>()
            .Single()
            .Should()
            .BeEquivalentTo(
                new
                {
                    Multiplier = 2,
                    DepositFurniCount = 0,
                    DepositCoinsCount = 0,
                    WithdrawalFurniCount = 0,
                    WithdrawalCoinsCount = 10,
                }
            );
        (
            (WiredChestWithdrawRequest)
                room.Fakes.Log.Of(nameof(IWiredChestGrain.WithdrawAsync)).Single().Args[0]!
        )
            .Amount.Should()
            .Be(10);
        var shown = room
            .Fakes.Sent<WiredTransactionSuccessMessageComposer>(SendRoute.Direct)
            .Single();
        shown.Contents.Type.Should().Be(WiredTransactionSuccessType.Rewarded);
        shown.Contents.RewardContents!.Nodes.Single().Amount.Should().Be(10);
        shown.Contents.RewardText.Should().Be("well done");
    }

    private sealed class HeardEvents : IRoomEventListener
    {
        public List<RoomEvent> Events { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            Events.Add(evt);

            return Task.CompletedTask;
        }
    }

    private static IWiredTradeGrain TradeGrain(Fakes fakes) =>
        (IWiredTradeGrain)GrainHarness.Create<WiredTradeGrain>(fakes, playerId: PLAYER);

    private static (RoomHarness Room, FurnitureWiredContractLogic Contract) CreateRoomWithContract(
        bool reward = false
    )
    {
        var room = new RoomHarness();
        room.State.IsRightsLoaded = true;

        // Furni tell the room when their stuff data changes.
        room.Room._roomOutbound = room.Fakes.Create<IAsyncStream<RoomOutboundSnapshot>>(
            "room-stream"
        );

        // The room's owner may modify its wired whatever the masks say.
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot)) with
        {
            RoomId = (RoomId)1,
            OwnerId = (PlayerId)1,
        };
        room.State.RoomSnapshot = info;

        var item = room.CreateFloorItem(
            CONTRACT,
            2,
            2,
            Altitude.Zero,
            name: reward ? "wf_contract_reward" : "wf_contract_payment",
            logic: reward ? "wired_contract_reward" : "wired_contract_payment",
            createLogic: (stuffDataFactory, ctx) =>
                reward
                    ? new FurnitureWiredRewardContractLogic(stuffDataFactory, ctx)
                    : new FurnitureWiredPaymentContractLogic(stuffDataFactory, ctx)
        );

        room.AddToRoom(item);

        return (room, (FurnitureWiredContractLogic)item.Logic);
    }

    private static WiredContractSnapshot Contract(
        ImmutableArray<TradeRequirementRuleSnapshot> give
    ) =>
        new()
        {
            ContractId = CONTRACT,
            Type = WiredContractType.Payment,
            Definition = new() { YouGive = give, YouGet = null },
            PaymentMode = WiredContractPaymentMode.Specific,
        };

    private static WiredContractTradeRequest Request(
        ImmutableArray<TradeRequirementRuleSnapshot> give,
        TradeRequirementRulesType rulesType,
        int multiplier
    ) =>
        new()
        {
            RoomId = 1,
            SourceId = CONTRACT,
            Contract = Contract(give),
            RulesType = rulesType,
            Multiplier = multiplier,
            ChestIds = [100],
            TimeoutSeconds = 0,
        };

    private static TradeRequirementRuleSnapshot Rule(params TradeRequirementNodeSnapshot[] nodes) =>
        new() { Nodes = [.. nodes] };

    private static TradeRequirementNodeSnapshot Furni(ChestItemTypeSnapshot type, int amount) =>
        new()
        {
            Type = TradeRequirementNodeType.Furni,
            Amount = amount,
            ItemType = type,
        };

    private static TradeRequirementNodeSnapshot Coins(int amount) =>
        new()
        {
            Type = TradeRequirementNodeType.Coin,
            Amount = amount,
            ItemType = null,
        };

    private static ChestItemTypeSnapshot Type(int sprite) =>
        new()
        {
            IsWallItem = false,
            TypeId = sprite,
            LegacyPosterId = "",
        };

    private static FurnitureItemSnapshot Item(int id, int sprite, string? name = null) =>
        new()
        {
            ItemId = id,
            SpriteId = sprite,
            OwnerId = PLAYER,
            OwnerName = "player",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = sprite,
                SpriteId = sprite,
                Name = name ?? $"furni{sprite}",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
                TotalStates = 1,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(100),
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = "",
            SecondsToExpiration = 0,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
}
