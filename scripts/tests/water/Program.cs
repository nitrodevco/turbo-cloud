using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Systems;

static class Program
{
    private static async Task Main()
    {
        Assert(
            WaterAreaMask.Build(1, 1, (x, y) => x == 1 && y == 0) == 1 << 3,
            "mask bit order right side"
        );
        Assert(WaterAreaMask.Build(2, 2, (_, _) => true) == 0xFFF, "mask ring width and length");
        var ringCoordinates = new[]
        {
            (2, 2),
            (1, 2),
            (0, 2),
            (-1, 2),
            (2, 1),
            (-1, 1),
            (2, 0),
            (-1, 0),
            (2, -1),
            (1, -1),
            (0, -1),
            (-1, -1),
        };
        for (var expected = 0; expected < 1 << ringCoordinates.Length; expected++)
        {
            var actual = WaterAreaMask.Build(
                2,
                2,
                (x, y) =>
                {
                    var bit = Array.IndexOf(ringCoordinates, (x, y));
                    return bit >= 0 && (expected & (1 << bit)) != 0;
                }
            );
            Assert(actual == expected, $"mask oracle {expected}");
        }
        AssertThrows(
            () => WaterAreaMask.Build(15, 15, static (_, _) => false),
            "mask rejects more than 32 ring bits"
        );

        var (room, state, system) = CreateRoom();
        var a = CreateWater(room, 1, 1, 0, Rotation.North, 1);
        var b = CreateWater(room, 2, 1, 0, Rotation.North, 2);
        Add(state, a);
        Add(state, b);

        await system
            .OnRoomEventAsync(
                new RoomItemAttatchedEvent { RoomId = 1, ObjectId = a.ObjectId },
                default
            )
            .ConfigureAwait(false);
        await system
            .OnRoomEventAsync(
                new RoomItemAttatchedEvent { RoomId = 1, ObjectId = b.ObjectId },
                default
            )
            .ConfigureAwait(false);
        Assert(a.Logic.GetState() == 1 << 3, "attach sets reciprocal right shore");
        Assert(b.Logic.GetState() == 1 << 4, "attach sets reciprocal left shore");

        var beforeMove = a.GetSnapshot();
        Move(b, 5, 1);
        await system
            .OnRoomEventAsync(
                new RoomItemMovedEvent
                {
                    RoomId = 1,
                    ObjectId = b.ObjectId,
                    PrevIdx = 1 * 8 + 2,
                },
                default
            )
            .ConfigureAwait(false);
        Assert(a.Logic.GetState() == 0, "move clears old neighbor shore");
        Assert(
            ((LegacyStuffSnapshot)a.GetSnapshot().StuffData).Data == "0"
                && !ReferenceEquals(beforeMove, a.GetSnapshot()),
            "reentry snapshot reflects derived state"
        );

        Move(b, 2, 1);
        b.SetPositionZ(Altitude.FromInt(1));
        await system
            .OnRoomEventAsync(
                new RoomItemMovedEvent
                {
                    RoomId = 1,
                    ObjectId = b.ObjectId,
                    PrevIdx = 1 * 8 + 5,
                },
                default
            )
            .ConfigureAwait(false);
        Assert(a.Logic.GetState() == 0, "different height does not join");

        b.SetPositionZ(Altitude.Zero);
        await system
            .OnRoomEventAsync(
                new RoomItemMovedEvent
                {
                    RoomId = 1,
                    ObjectId = b.ObjectId,
                    PrevIdx = 1 * 8 + 5,
                },
                default
            )
            .ConfigureAwait(false);
        Assert(a.Logic.GetState() == 1 << 3, "same height rejoins");

        await system
            .OnRoomEventAsync(
                new RoomItemDetachedEvent { RoomId = 1, ObjectId = b.ObjectId },
                default
            )
            .ConfigureAwait(false);
        Assert(a.Logic.GetState() == 0, "detach clears neighbor shore");

        var distinctDefinition = CreateWater(room, 2, 1, 0, Rotation.North, 5, definitionId: 200);
        Add(state, distinctDefinition);
        await system
            .OnRoomEventAsync(
                new RoomItemAttatchedEvent { RoomId = 1, ObjectId = distinctDefinition.ObjectId },
                default
            )
            .ConfigureAwait(false);
        Assert(a.Logic.GetState() == 0, "different definition does not join");
        Assert(distinctDefinition.Logic.GetState() == 0, "different definition keeps its border");

        var rotated = CreateWater(room, 1, 4, 0, Rotation.East, 3, 2, 1);
        var rotatedNeighbor = CreateWater(room, 1, 6, 0, Rotation.North, 4);
        Add(state, rotated);
        Add(state, rotatedNeighbor);
        var extraDataBeforePickup = rotated.ExtraData.GetJsonString();
        var dirtyCallbackCount = 0;
        rotated.SetAction(_ => dirtyCallbackCount++);
        await system
            .OnRoomEventAsync(
                new RoomItemAttatchedEvent { RoomId = 1, ObjectId = rotated.ObjectId },
                default
            )
            .ConfigureAwait(false);
        await system
            .OnRoomEventAsync(
                new RoomItemAttatchedEvent { RoomId = 1, ObjectId = rotatedNeighbor.ObjectId },
                default
            )
            .ConfigureAwait(false);
        Assert(rotated.Logic.GetState() == 1 << 1, "rotation uses swapped footprint");
        ((FurnitureWaterAreaLogic)rotated.Logic).ResetPickedUpState();
        Assert(rotated.Logic.GetState() == 0, "pickup resets transient state");
        Assert(
            rotated.ExtraData.GetJsonString() == extraDataBeforePickup,
            "extra data is unchanged"
        );
        Assert(dirtyCallbackCount == 0, "derived state does not invoke dirty callback");

        Console.WriteLine("water joining harness passed");
    }

    private static (RoomGrain Room, object State, RoomWaterAreaSystem System) CreateRoom()
    {
        var room = (RoomGrain)RuntimeHelpers.GetUninitializedObject(typeof(RoomGrain));
        var stateType = typeof(RoomGrain).Assembly.GetType("Turbo.Rooms.Grains.RoomLiveState")!;
        var state = Activator.CreateInstance(stateType, nonPublic: true)!;
        SetField(room, "_state", state);

        var model = new RoomModelSnapshot
        {
            Id = 1,
            Name = "test",
            Model = "",
            DoorX = 0,
            DoorY = 0,
            DoorRotation = Rotation.North,
            Width = 8,
            Height = 8,
            Size = 64,
            BaseHeights = new Altitude[64],
            BaseFlags = Enumerable.Repeat(RoomTileFlags.Walkable, 64).ToArray(),
        };
        stateType.GetProperty("Model")!.SetValue(state, model);
        var map = new RoomMapModule(room);
        SetField(room, "MapModule", map);
        map.GetType()
            .GetMethod("EnsureMapBuiltAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(map, [default(CancellationToken)]);
        return (room, state, new RoomWaterAreaSystem(room));
    }

    private static RoomFloorItem CreateWater(
        RoomGrain room,
        int x,
        int y,
        int z,
        Rotation rotation,
        int id,
        int width = 1,
        int length = 1,
        int definitionId = 100
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = definitionId,
                SpriteId = id,
                Name = "bw_water_1",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Floor,
                LogicName = "furniture_water_area",
                TotalStates = 1,
                Width = width,
                Length = length,
                StackHeight = Altitude.Zero,
                CanStack = true,
                CanWalk = true,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Nobody,
                ExtraData = null,
            },
        };
        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.FromInt(z));
        item.SetRotation(rotation);
        var context = new RoomFloorItemContext(room, item);
        var factory = new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance);
        item.SetLogic(new FurnitureWaterAreaLogic(factory, context));
        return item;
    }

    private static void Add(object state, RoomFloorItem item)
    {
        var items = (IDictionary)state.GetType().GetProperty("ItemsById")!.GetValue(state)!;
        items.Add(item.ObjectId, item);
    }

    private static void Move(RoomFloorItem item, int x, int y) => item.SetPosition(x, y);

    private static void SetField(object target, string name, object value) =>
        target
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private static void Assert(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"FAIL: {name}");
    }

    private static void AssertThrows(Action action, string name)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException($"FAIL: {name}");
    }
}
