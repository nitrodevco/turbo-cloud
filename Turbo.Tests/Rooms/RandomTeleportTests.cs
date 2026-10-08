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
/// The in-room random teleport (the Banzai teleporter), through the room's walk path: stepping
/// onto one sends the avatar to another of them, and landing there does not send it back.
/// </summary>
public sealed class RandomTeleportTests
{
    private const int TELE_A = 80;
    private const int TELE_B = 81;
    private const int OWNER_AVATAR = 1;
    private const int OWNER = 100 + OWNER_AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private long _now;

    public RandomTeleportTests()
    {
        RoomHarness.SetField(
            _room.Harness.Room,
            "_roomConfig",
            new RoomConfig { RandomTeleportFlashMs = 60_000 }
        );
        _room.Harness.Fakes.Handlers["get_RoomObject"] = call =>
            call.Interface == typeof(IRoomPlayerContext)
            && call.Key is int id
            && _room.Avatars.TryGetValue(id, out var avatar)
                ? avatar
                : Fakes.NotHandled;
    }

    [Fact]
    public async Task Running_across_a_teleporter_sends_the_avatar_to_the_other_and_no_further()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        var a = AddTele(TELE_A, 2, 3);
        var b = AddTele(TELE_B, 7, 7);

        await WalkToAsync(avatar, 4, 3);

        (avatar.X, avatar.Y).Should().Be((7, 7));
        avatar.IsWalking.Should().BeFalse();
        (a.Logic.GetState(), b.Logic.GetState()).Should().Be((1, 1), "both light up");

        // Landing is not a step onto it: the avatar stays, however long the room runs.
        for (var run = 0; run < 3; run++)
        {
            await RunTimersAsync();
            (avatar.X, avatar.Y).Should().Be((7, 7), $"after {run + 1} more timer runs");
        }
    }

    [Fact]
    public async Task With_no_other_teleporter_to_go_to_nothing_happens()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        AddTele(TELE_A, 2, 3);

        await WalkToAsync(avatar, 4, 3);

        (avatar.X, avatar.Y).Should().Be((4, 3));
    }

    private IRoomFloorItem AddTele(int objectId, int x, int y) =>
        AddFurni(
            objectId,
            x,
            y,
            "random_teleport",
            "",
            (s, c) => new FurnitureRandomTeleportLogic(s, c),
            totalStates: 2,
            canWalk: true
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
        bool canWalk = false
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
                Width = 1,
                Length = 1,
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
