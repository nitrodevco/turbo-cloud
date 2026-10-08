using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
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
/// The Black Hole, a 2x2 hole in the floor, through the room's use and walk paths: open (state
/// 0) nobody can stand on any of its tiles, closed it is floor, and it does not open under
/// someone.
/// </summary>
public sealed class FloorHoleTests
{
    private const int HOLE = 90;
    private const int OWNER_AVATAR = 1;
    private const int OWNER = 100 + OWNER_AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private long _now;

    public FloorHoleTests()
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

    [Fact]
    public async Task An_open_hole_cannot_be_stood_on_anywhere_and_a_closed_one_can()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 1);
        AddHole();

        foreach (var (x, y) in new[] { (3, 3), (4, 3), (3, 4), (4, 4) })
            (await WalkRequestAsync(x, y)).Should().BeFalse($"({x}, {y}) is open hole");

        await UseAsync();
        await WalkToAsync(avatar, 4, 4);

        (avatar.X, avatar.Y).Should().Be((4, 4), "a closed hole is floor");
    }

    [Fact]
    public async Task A_hole_does_not_open_under_someone_standing_on_it()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 1);
        var hole = AddHole();

        await UseAsync();
        await WalkToAsync(avatar, 4, 4);
        await UseAsync();

        hole.Logic.GetState().Should().Be(1);
    }

    private Task<bool> WalkRequestAsync(int x, int y) =>
        _room.Harness.Room.WalkAvatarToAsync(ActionContext.CreateForPlayer(OWNER, 1), x, y, Ct);

    private Task UseAsync() =>
        _room.Harness.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(OWNER, 1), HOLE, Ct);

    private IRoomFloorItem AddHole() =>
        AddFurni(
            HOLE,
            3,
            3,
            "floor_hole",
            "",
            (s, c) => new FurnitureFloorHoleLogic(s, c),
            totalStates: 2,
            canWalk: true,
            size: 2
        );

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
            await RunTimersAsync();
            onEachStep?.Invoke();
        }

        await RunTimersAsync();
    }

    private async Task WalkUntilStoppedAsync(RoomPlayerAvatar avatar)
    {
        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }

        await RunTimersAsync();
    }

    // One room tick ahead: far enough for the furni's own timers, short of any expiry.
    private Task RunTimersAsync() =>
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

    private IRoomFloorItem AddFurni(
        int objectId,
        int x,
        int y,
        string logicName,
        string customParams,
        Func<IStuffDataFactory, IRoomFloorItemContext, FurnitureFloorLogic> logic,
        int totalStates = 1,
        FurnitureUsageType usage = FurnitureUsageType.Controller,
        bool canWalk = false,
        int size = 1
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
                Name = logicName,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = logicName,
                TotalStates = totalStates,
                Width = size,
                Length = size,
                StackHeight = Altitude.FromInt(canWalk ? 0 : 100),
                CanStack = canWalk,
                CanWalk = canWalk,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = usage,
                CustomParams = customParams,
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);
        item.SetLogic(logic(_room.StuffData, new RoomFloorItemContext(_room.Harness.Room, item)));

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
