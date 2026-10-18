using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Glass Door and the Wall Of Flame through the room's use, walk and wired paths, as the
/// official client capture of 2026-10-17 has them: state 0 blocks the tile (a walk onto it is
/// refused, one across goes round), state 1 is floor at the door's own height, and neither a use
/// nor wired changes a door someone stands in.
/// </summary>
public sealed class WiredDoorTests
{
    private const int DOOR = 90;
    private const int OTHER_DOOR = 91;
    private const int OWNER_AVATAR = 1;
    private const int OWNER = 100 + OWNER_AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private long _now;

    public WiredDoorTests()
    {
        var snapshot = RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "OwnerId", (PlayerId)OWNER);
        _room.Harness.Fakes.Handlers["get_RoomObject"] = call =>
            call.Interface == typeof(IRoomPlayerContext)
            && call.Key is int id
            && _room.Avatars.TryGetValue(id, out var avatar)
                ? avatar
                : Fakes.NotHandled;
    }

    [Theory]
    [InlineData("wf_glassdoor", typeof(FurnitureWiredDoorLogic))]
    [InlineData("wf_firegate", typeof(FurnitureFireGateLogic))]
    public void Each_door_gets_the_door_logic_by_its_name(string name, Type logic)
    {
        var item = _room.AddFloorItem(40, 1, 1, name);

        _room
            .Harness.LogicProvider.CreateLogicInstance(
                name,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
            .Should()
            .BeOfType(logic);
    }

    [Theory]
    [InlineData("wf_glassdoor", 0.2)]
    [InlineData("wf_firegate", 0.0)]
    public async Task A_shut_door_blocks_its_tile_and_an_open_one_is_stood_on_at_its_height(
        string name,
        double height
    )
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        AddDoor(DOOR, 3, 3, name, height);

        (await WalkRequestAsync(3, 3)).Should().BeFalse("state 0 blocks the door's tile");

        await WalkToAsync(
            avatar,
            5,
            3,
            onEachStep: () =>
                (avatar.X, avatar.Y).Should().NotBe((3, 3), "a walk across a shut door goes round")
        );
        (avatar.X, avatar.Y).Should().Be((5, 3));

        await UseAsync(DOOR);
        await WalkToAsync(avatar, 3, 3);

        (avatar.X, avatar.Y).Should().Be((3, 3), "state 1 is floor");
        avatar.Z.Value.Should().BeApproximately(height, 0.001);
    }

    [Fact]
    public async Task A_use_switches_between_two_states_whatever_the_definition_counts()
    {
        var door = AddDoor(DOOR, 3, 3, "wf_glassdoor", 0.2, totalStates: 3);

        await UseAsync(DOOR);
        door.Logic.GetState().Should().Be(1);

        await UseAsync(DOOR);
        door.Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task A_use_does_not_shut_a_door_someone_stands_in()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        var door = AddDoor(DOOR, 3, 3, "wf_glassdoor", 0.2);

        await UseAsync(DOOR);
        await WalkToAsync(avatar, 3, 3);
        await UseAsync(DOOR);

        door.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task Wired_leaves_a_door_someone_stands_in_out_of_its_state_update()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        var stoodIn = AddDoor(DOOR, 3, 3, "wf_glassdoor", 0.2);
        var free = AddDoor(OTHER_DOOR, 6, 6, "wf_firegate", 0);

        await UseAsync(DOOR);
        await WalkToAsync(avatar, 3, 3);

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        foreach (var door in new[] { stoodIn, free })
            await ctx.ProcessItemStateUpdateAsync(door, door.Logic.GetNextToggleableState());

        stoodIn.Logic.GetState().Should().Be(1);
        free.Logic.GetState().Should().Be(1);
        ctx.FloorItemStateUpdates.Select(x => (int)x.Item1).Should().Equal(OTHER_DOOR);
    }

    private Task<bool> WalkRequestAsync(int x, int y) =>
        _room.Harness.Room.WalkAvatarToAsync(ActionContext.CreateForPlayer(OWNER, 1), x, y, Ct);

    private Task UseAsync(int id) =>
        _room.Harness.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(OWNER, 1), id, Ct);

    /// <summary>Walks the avatar there tick by tick, running the room's timers between ticks.</summary>
    private async Task WalkToAsync(RoomPlayerAvatar avatar, int x, int y, Action? onEachStep = null)
    {
        (
            await _room.Harness.Room.WalkAvatarToAsync(
                ActionContext.CreateForPlayer(100 + (int)avatar.ObjectId, 1),
                x,
                y,
                Ct
            )
        )
            .Should()
            .BeTrue();

        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            onEachStep?.Invoke();
        }
    }

    private IRoomFloorItem AddDoor(
        int objectId,
        int x,
        int y,
        string name,
        double stackHeight,
        int totalStates = 2
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = objectId,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + objectId,
                SpriteId = 1000 + objectId,
                Name = name,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = name,
                TotalStates = totalStates,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromValue(stackHeight),
                CanStack = false,
                CanWalk = true,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Controller,
                CustomParams = "",
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);

        var context = new RoomFloorItemContext(_room.Harness.Room, item);

        item.SetLogic(
            name == "wf_firegate"
                ? new FurnitureFireGateLogic(_room.StuffData, context)
                : new FurnitureWiredDoorLogic(_room.StuffData, context)
        );

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Add(
            (RoomObjectId)objectId,
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
