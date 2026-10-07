using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who may do what with a wired chest in a room, and the trade window that fills one:
/// a chest is locked when placed, only its owner takes from a plain chest, and a deposit goes
/// through only once accepted and confirmed, with only what the chest takes.
/// </summary>
public class WiredChestRoomTests
{
    private const int CHEST = 7;
    private const int OWNER = 1;
    private const int GUEST = 5;

    [Fact]
    public async Task A_chest_is_locked_when_placed()
    {
        var (room, chest) = CreateRoomWithChest();

        await chest.OnPlaceAsync(ActionContext.CreateForPlayer(OWNER, 1), default);

        chest.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task A_guest_cannot_take_from_a_plain_chest()
    {
        var (room, _) = CreateRoomWithChest();
        AddPlayer(room, GUEST);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(GUEST, 1),
            CHEST,
            new WithdrawFromChestInteraction { Amount = null, ItemType = null },
            default
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IWiredChestGrain.WithdrawAsync)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_owner_takes_from_the_chest_and_is_told_how_it_went()
    {
        var (room, _) = CreateRoomWithChest();
        AddPlayer(room, OWNER);
        room.Fakes.Handlers[nameof(IWiredChestGrain.WithdrawAsync)] = _ =>
            Task.FromResult(
                WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.Empty,
                    WiredChestSummarySnapshot.Empty
                )
            );

        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            new WithdrawFromChestInteraction { Amount = null, ItemType = null },
            default
        );

        room.Fakes.Log.Of(nameof(IWiredChestGrain.WithdrawAsync)).Should().ContainSingle();
        Sent(room.Fakes)
            .OfType<WiredTransactionFailMessageComposer>()
            .Should()
            .ContainSingle(x => x.FailureType == WiredTransactionFailureType.Empty);
    }

    [Fact]
    public async Task A_furni_chest_trade_does_not_take_credit_furni()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("CF_10_goldbar"));

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, default);
        await trade.AddItemsAsync([20], default);

        Sent(fakes)
            .OfType<WiredTradeTransactionNotificationMessageComposer>()
            .Should()
            .ContainSingle(x => x.Error == WiredTradeErrorType.InvalidItem);
        Sent(fakes)
            .OfType<WiredTradeItemsUpdateMessageComposer>()
            .Last()
            .FirstItems.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task A_trade_is_deposited_only_after_it_was_accepted_and_confirmed()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("chair"));
        fakes.Handlers[nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)] = _ =>
            Task.FromResult(
                new WiredChestMoveResultSnapshot
                {
                    Failure = null,
                    TransactionId = 1,
                    Coins = 0,
                    Items = [],
                    Summary = WiredChestSummarySnapshot.Empty,
                }
            );

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, default);
        await trade.AddItemsAsync([20], default);

        await trade.ConfirmAsync(isFinalConfirm: true, default);
        fakes.Log.Of(nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)).Should().BeEmpty();

        await trade.ConfirmAsync(isFinalConfirm: false, default);
        await trade.ConfirmAsync(isFinalConfirm: true, default);

        var deposit = fakes.Log.Of(nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)).Single();
        ((ImmutableArray<RoomObjectId>)deposit.Args[2]!).Should().Equal((RoomObjectId)20);
        Sent(fakes).OfType<WiredTradeCompletedMessageComposer>().Should().ContainSingle();
    }

    /// <summary>Names the room grain method the trade grain calls; nameof needs a type.</summary>
    private interface IRoomGrainDeposit
    {
        public Task DepositIntoWiredChestAsync();
    }

    private static IWiredTradeGrain TradeGrain(Fakes fakes) =>
        (IWiredTradeGrain)
            GrainHarness.Create(
                typeof(RoomGrain).Assembly,
                "Turbo.Rooms.Grains.WiredTrading.WiredTradeGrain",
                fakes,
                playerId: GUEST
            );

    /// <summary>Every composer sent to a player, single or batched.</summary>
    private static IEnumerable<IComposer> Sent(Fakes fakes) =>
        fakes.Log.Calls.SelectMany(call =>
            call.Method is "SendComposerAsync" or "SendComposerToPlayerAsync"
                ? call
                    .Args.OfType<IComposer>()
                    .Concat(call.Args.OfType<IEnumerable<IComposer>>().SelectMany(x => x))
                : []
        );

    private static ImmutableArray<FurnitureItemSnapshot> Offer(string name) =>
        [
            new FurnitureItemSnapshot
            {
                ItemId = 20,
                SpriteId = 20,
                OwnerId = GUEST,
                OwnerName = "guest",
                Definition = new FurnitureDefinitionSnapshot
                {
                    Id = 20,
                    SpriteId = 20,
                    Name = name,
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
            },
        ];

    private static void AddPlayer(RoomHarness room, int playerId)
    {
        var player = room.Fakes.Create<IRoomPlayer>(playerId);

        room.Fakes.Handlers["get_PlayerId"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? (PlayerId)id
                : Fakes.NotHandled;
        room.Fakes.Handlers["get_Name"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? $"player{id}"
                : Fakes.NotHandled;

        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(room.State, "AvatarsByPlayerId")!
        )[playerId] = playerId;
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(room.State, "AvatarsByObjectId")!
        )[playerId] = player;
    }

    private static (RoomHarness Room, FurnitureWiredChestLogic Chest) CreateRoomWithChest()
    {
        var room = new RoomHarness();

        // The chest tells the room when its stuff data changes.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        var item = room.CreateFloorItem(
            CHEST,
            2,
            2,
            Altitude.Zero,
            name: "wf_storage_furni1",
            logic: "wired_chest_furni",
            createLogic: (stuffDataFactory, ctx) =>
                new FurnitureWiredFurniChestLogic(stuffDataFactory, ctx)
        );

        room.AddToRoom(item);

        return (room, (FurnitureWiredChestLogic)item.Logic);
    }
}
