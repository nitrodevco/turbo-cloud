using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;
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
    public async Task A_contract_with_more_options_than_the_editor_allows_is_not_saved()
    {
        var (room, contract) = CreateRoomWithContract();
        var tooMany = Contract([.. Enumerable.Repeat(Rule(Coins(1)), 4)]);

        var saved = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(1, 1),
            CONTRACT,
            new UpdateContractInteraction { Contract = tooMany },
            default
        );

        saved.Should().BeFalse();
        contract.Contract.Definition.YouGive!.Value.Should().BeEmpty();
        Sent(room.Fakes)
            .OfType<WiredContractUpdateResultMessageComposer>()
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
            default
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
            default
        );
        await trade.AddItemsAsync([1], default);

        // One chair of two: the trade cannot be accepted yet.
        await trade.ConfirmAsync(false, default);
        await trade.ConfirmAsync(true, default);
        fakes.Log.Of("CompleteWiredContractTradeAsync").Should().BeEmpty();
        Sent(fakes)
            .OfType<WiredTradeItemsUpdateMessageComposer>()
            .Last()
            .CanAccept.Should()
            .BeFalse();

        await trade.AddItemsAsync([2], default);
        await trade.ConfirmAsync(false, default);
        await trade.ConfirmAsync(true, default);

        var completed = fakes.Log.Of("CompleteWiredContractTradeAsync").Single();
        ((ImmutableArray<FurnitureItemSnapshot>)completed.Args[2]!)
            .Select(x => x.ItemId.Value)
            .Should()
            .BeEquivalentTo([1, 2]);
        Sent(fakes)
            .OfType<WiredTransactionSuccessMessageComposer>()
            .Should()
            .ContainSingle(x => x.Contents.Type == WiredTransactionSuccessType.Payment);
    }

    [Fact]
    public async Task A_second_contract_is_refused_while_the_player_is_trading()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var request = Request([Rule(Coins(10))], TradeRequirementRulesType.Single, 1);

        await trade.StartContractAsync(request, default);
        await trade.StartContractAsync(request, default);

        Sent(fakes).OfType<WiredTradeInitiateMessageComposer>().Should().ContainSingle();
        fakes
            .Log.Of("ReportWiredTransactionFailedAsync")
            .Should()
            .ContainSingle(x =>
                (WiredTransactionFailureType)x.Args[2]!
                == WiredTransactionFailureType.AlreadyTrading
            );
    }

    [Fact]
    public async Task A_trade_wired_cancelled_is_replaced_without_closing_the_window()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        var request = Request([Rule(Coins(10))], TradeRequirementRulesType.Single, 1);

        await trade.StartContractAsync(request, default);
        await trade.CancelByWiredAsync([], default);
        await trade.StartContractAsync(request, default);

        Sent(fakes).OfType<WiredTradeCancelledMessageComposer>().Should().BeEmpty();
        Sent(fakes)
            .OfType<WiredTradeInitiateMessageComposer>()
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

        var failure = await room.Room.WiredTransactionSystem.RewardAsync(
            player,
            [chest],
            reward,
            CONTRACT,
            times: 2,
            default
        );

        failure.Should().BeNull();
        (
            (WiredChestWithdrawRequest)
                room.Fakes.Log.Of(nameof(IWiredChestGrain.WithdrawAsync)).Single().Args[0]!
        )
            .Amount.Should()
            .Be(10);
        var shown = Sent(room.Fakes).OfType<WiredTransactionSuccessMessageComposer>().Single();
        shown.Contents.Type.Should().Be(WiredTransactionSuccessType.Rewarded);
        shown.Contents.RewardContents!.Nodes.Single().Amount.Should().Be(10);
        shown.Contents.RewardText.Should().Be("well done");
    }

    private static IWiredTradeGrain TradeGrain(Fakes fakes) =>
        (IWiredTradeGrain)
            GrainHarness.Create(
                typeof(RoomGrain).Assembly,
                "Turbo.Rooms.Grains.WiredTrading.WiredTradeGrain",
                fakes,
                playerId: PLAYER
            );

    private static IEnumerable<IComposer> Sent(Fakes fakes) =>
        fakes.Log.Calls.SelectMany(call =>
            call.Method is "SendComposerAsync" or "SendComposerToPlayerAsync"
                ? call
                    .Args.OfType<IComposer>()
                    .Concat(call.Args.OfType<IEnumerable<IComposer>>().SelectMany(x => x))
                : []
        );

    private static (RoomHarness Room, FurnitureWiredContractLogic Contract) CreateRoomWithContract()
    {
        var room = new RoomHarness();
        RoomHarness.SetMember(room.State, "IsRightsLoaded", true);

        // Furni tell the room when their stuff data changes.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        // The room's owner may modify its wired whatever the masks say.
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(info, "RoomId", (RoomId)1);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)1);
        RoomHarness.SetMember(room.State, "RoomSnapshot", info);

        var item = room.CreateFloorItem(
            CONTRACT,
            2,
            2,
            Altitude.Zero,
            name: "wf_contract_payment",
            logic: "wired_contract_payment",
            createLogic: (stuffDataFactory, ctx) =>
                new FurnitureWiredPaymentContractLogic(stuffDataFactory, ctx)
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
            RoomId = 0,
        };
}
