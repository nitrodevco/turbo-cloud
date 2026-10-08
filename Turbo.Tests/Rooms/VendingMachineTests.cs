using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Vending furni through the room's use path, as a double-click reaches it: a fridge facing east
/// at (3, 3), so its front is (4, 3). It walks the avatar there, turns it to face the fridge,
/// shows the dispensing state and hands over one of its items (Sulake's <c>&lt;drinks&gt;</c>).
/// </summary>
public sealed class VendingMachineTests
{
    private const int FRIDGE = 60;
    private const int AVATAR = 1;
    private const int PLAYER = 100 + AVATAR;
    private const int GUEST_AVATAR = 2;
    private const int GUEST = 100 + GUEST_AVATAR;
    private static readonly int[] DRINKS = [3, 4, 5, 6];

    private const string PURA_FRIDGE = """{"vending":{"handItems":[3,4,5,6]}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private long _now;

    public VendingMachineTests()
    {
        // The dispensing state is shown for no time at all here, so one timer run ends it.
        RoomHarness.SetField(
            _room.Harness.Room,
            "_roomConfig",
            new RoomConfig { VendingDispenseMs = 0 }
        );
        var snapshot = RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "OwnerId", (PlayerId)PLAYER);
    }

    [Fact]
    public async Task An_avatar_in_front_is_served_at_once_facing_the_fridge()
    {
        var avatar = _room.Enter(AVATAR, 4, 3);
        var fridge = AddFridge(PURA_FRIDGE);

        await UseAsync(PLAYER);

        DRINKS.Should().Contain(avatar.HandItemId);
        avatar.Rotation.Should().Be(Rotation.West);
        fridge.Logic.GetState().Should().Be(1, "it shows its dispensing state");

        await RunFurniTimersAsync();

        fridge.Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task A_use_from_across_the_room_walks_over_to_its_front_and_serves_on_arrival()
    {
        var avatar = _room.Enter(AVATAR, 8, 8);
        AddFridge(PURA_FRIDGE);

        await UseAsync(PLAYER);

        avatar.HandItemId.Should().Be(0);
        _room.Map.GetTileXY(avatar.GoalTileId).Should().Be((4, 3));

        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        (avatar.X, avatar.Y).Should().Be((4, 3));
        DRINKS.Should().Contain(avatar.HandItemId);
        avatar.Rotation.Should().Be(Rotation.West);
    }

    [Fact]
    public async Task With_its_front_blocked_it_serves_from_the_nearest_tile_beside_it()
    {
        var avatar = _room.Enter(AVATAR, 3, 8);
        _room.AddFloorItem(70, 4, 3, canWalk: false);
        AddFridge(PURA_FRIDGE);

        await UseAsync(PLAYER);
        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        (avatar.X, avatar.Y).Should().Be((3, 4));
        DRINKS.Should().Contain(avatar.HandItemId);
        avatar.Rotation.Should().Be(Rotation.North);
    }

    [Fact]
    public async Task Walking_off_somewhere_else_on_the_way_is_not_served()
    {
        var avatar = _room.Enter(AVATAR, 8, 8);
        AddFridge(PURA_FRIDGE);

        await UseAsync(PLAYER);
        await _room.Harness.Room.WalkAvatarToAsync(
            ActionContext.CreateForPlayer(PLAYER, 1),
            8,
            1,
            Ct
        );
        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        avatar.HandItemId.Should().Be(0);
    }

    [Fact]
    public async Task A_vending_furni_only_rights_may_use_serves_no_visitor()
    {
        var guest = _room.Enter(GUEST_AVATAR, 4, 3);
        AddFridge(PURA_FRIDGE, usage: FurnitureUsageType.Controller);

        await UseAsync(GUEST);

        guest.HandItemId.Should().Be(0);
    }

    [Fact]
    public async Task A_static_provider_hands_the_item_over_without_a_dispensing_state()
    {
        var avatar = _room.Enter(AVATAR, 4, 3);
        var bowl = AddFridge("""{"vending":{"handItems":[112],"animates":false}}""");

        await UseAsync(PLAYER);

        avatar.HandItemId.Should().Be(112);
        bowl.Logic.GetState().Should().Be(0);
    }

    private Task UseAsync(int playerId) =>
        _room.Harness.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(playerId, 1), FRIDGE, Ct);

    private async Task WalkUntilStoppedAsync(RoomPlayerAvatar avatar)
    {
        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }
    }

    // One room tick ahead: far enough for the fridge's own tick, short of the hand item's expiry.
    private Task RunFurniTimersAsync() =>
        _room
            .Harness.Module<RoomTimerSystem>()
            .ProcessTimersAsync(
                (long)(
                    System.Diagnostics.Stopwatch.GetTimestamp()
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency
                ) + 500,
                Ct
            );

    private IRoomFloorItem AddFridge(
        string definitionExtraData,
        FurnitureUsageType usage = FurnitureUsageType.Everybody
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = FRIDGE,
            OwnerId = PLAYER,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1060,
                SpriteId = 1060,
                Name = "fridge",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "vending_machine",
                TotalStates = 2,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(100),
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = usage,
                ExtraData = definitionExtraData,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(3, 3);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.East);
        item.SetLogic(
            new FurnitureVendingMachineLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Add(
            (RoomObjectId)FRIDGE,
            item
        );
        typeof(RoomMapModule)
            .GetMethod(
                "AddItem",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                [typeof(IRoomItem)]
            )!
            .Invoke(_room.Map, [item]);

        return item;
    }
}
