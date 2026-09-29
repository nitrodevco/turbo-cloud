using EvalHarness;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests for water shorelines. Items come into a room built by its real
/// constructor through the one placement entry point, with the definition data the database
/// holds (logic "default_floor"), and the tests read what the client would get: each item's
/// state and its wire snapshot. Bit layout per the issue (1x1 ring: 0 SE, 1 S, 2 SW, 3 E,
/// 4 W, 5 NE, 6 N, 7 NW).
/// </summary>
public class WaterJoiningTests
{
    private const int E = 1 << 3, W = 1 << 4, S = 1 << 1, N = 1 << 6;

    private static ActionContext Ctx() => new() { Origin = ActionOrigin.Player, PlayerId = 1, RoomId = 1 };

    private static int _nextId = 100;

    private static RoomFloorItem Item(string name, int defId, int width = 1, int length = 1, bool canStack = true)
    {
        var id = Interlocked.Increment(ref _nextId);
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "eval",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = defId, SpriteId = defId, Name = name, ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default, LogicName = "default_floor",
                TotalStates = 1, Width = width, Length = length, StackHeight = Altitude.Zero,
                CanStack = canStack, CanWalk = true, CanSit = false, CanLay = false,
                CanRecycle = true, CanTrade = true, CanGroup = true, CanSell = true,
                UsagePolicy = FurnitureUsageType.Nobody, ExtraData = null,
            },
        };
        item.SetExtraData(null);
        return item;
    }

    private static RoomFloorItem Water(string name = "bw_water_1", int w = 1, int l = 1) =>
        Item(name, name switch { "bw_water_1" => 501, "bw_water_2" => 502, "val13_water" => 503, _ => 504 }, w, l);

    private static async Task Place(LiveRoomHarness h, RoomFloorItem item, int x, int y, Rotation rot = Rotation.North, double? z = null)
    {
        var ok = await h.Module<RoomFurniModule>().PlaceFloorItemAsync(
            Ctx(), item, x, y, rot, CancellationToken.None, z is null ? null : Altitude.FromValue(z.Value));
        Assert.True(ok, $"placing {item.Definition.Name} at {x},{y}");
    }

    private static Task Move(LiveRoomHarness h, RoomFloorItem item, int x, int y) =>
        h.Module<RoomFurniModule>().MoveFloorItemAsync(
            Ctx(), item, h.Module<RoomMapModule>().ToIdx(x, y), null, null, true, CancellationToken.None);

    private static int State(RoomFloorItem item) => item.Logic.GetState();

    [Theory]
    [InlineData("bw_water_1")]
    [InlineData("bw_water_2")]
    [InlineData("val13_water")]
    [InlineData("stackable_water")]
    public async Task SameWaterSideBySide_JoinsBothWays(string name)
    {
        var h = new LiveRoomHarness();
        var a = Water(name);
        var b = Water(name);
        await Place(h, a, 2, 2);
        await Place(h, b, 3, 2);

        Assert.Equal(E, State(a));
        Assert.Equal(W, State(b));
    }

    [Fact]
    public async Task SurroundedWater_HasEveryRingBit()
    {
        var h = new LiveRoomHarness();
        var center = Water();
        await Place(h, center, 4, 4);
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    await Place(h, Water(), 4 + dx, 4 + dy);

        Assert.Equal(0xFF, State(center));
    }

    [Fact]
    public async Task VerticalNeighbours_UseSouthAndNorthBits()
    {
        var h = new LiveRoomHarness();
        var top = Water();
        var bottom = Water();
        await Place(h, top, 5, 2);
        await Place(h, bottom, 5, 3);

        Assert.Equal(S, State(top));
        Assert.Equal(N, State(bottom));
    }

    [Fact]
    public async Task DifferentWaterTypes_KeepTheirBorders()
    {
        var h = new LiveRoomHarness();
        var shallow = Water("bw_water_1");
        var deep = Water("bw_water_2");
        await Place(h, shallow, 2, 2);
        await Place(h, deep, 3, 2);

        Assert.Equal(0, State(shallow));
        Assert.Equal(0, State(deep));
    }

    [Fact]
    public async Task DifferentHeights_DoNotJoin()
    {
        var h = new LiveRoomHarness();
        var low = Water();
        var high = Water();
        await Place(h, low, 2, 2);
        await Place(h, high, 3, 2, z: 1.0);

        Assert.Equal(0, State(low));
        Assert.Equal(0, State(high));
    }

    [Fact]
    public async Task MovingAway_ClearsTheOldNeighbour_AndMovingBackRejoins()
    {
        var h = new LiveRoomHarness();
        var a = Water();
        var b = Water();
        await Place(h, a, 2, 2);
        await Place(h, b, 3, 2);

        await Move(h, b, 7, 7);
        Assert.Equal(0, State(a));
        Assert.Equal(0, State(b));

        await Move(h, b, 1, 2);
        Assert.Equal(W, State(a));
        Assert.Equal(E, State(b));
    }

    [Fact]
    public async Task PickingUpANeighbour_ClearsTheShore()
    {
        var h = new LiveRoomHarness();
        var a = Water();
        var b = Water();
        await Place(h, a, 2, 2);
        await Place(h, b, 3, 2);

        await h.Module<RoomActionModule>().RemoveItemByIdAsync(Ctx(), b.ObjectId, CancellationToken.None);

        Assert.Equal(0, State(a));
    }

    [Fact]
    public async Task NewEntrantSnapshot_CarriesTheCurrentShore()
    {
        var h = new LiveRoomHarness();
        var a = Water();
        _ = a.Logic; // no logic until placed
        await Place(h, a, 2, 2);
        _ = a.GetSnapshot(); // cached before the neighbour arrives
        await Place(h, Water(), 3, 2);

        var stuff = Assert.IsType<LegacyStuffSnapshot>(a.GetSnapshot().StuffData);
        Assert.Equal(E.ToString(), stuff.Data);
    }

    [Fact]
    public async Task OtherFurniNextToWater_DoesNotJoin()
    {
        var h = new LiveRoomHarness();
        var water = Water();
        var chair = Item("chair_basic", 900);
        await Place(h, water, 2, 2);
        await Place(h, chair, 3, 2);

        Assert.Equal(0, State(water));
    }

    [Fact]
    public async Task LargerFootprint_UsesItsWholeRing()
    {
        // 2x2 ring (12 bits): y=2 row x=2..-1 -> bits 0-3; y=1: x=2 bit 4, x=-1 bit 5;
        // y=0: x=2 bit 6, x=-1 bit 7; y=-1 row x=2..-1 -> bits 8-11.
        var h = new LiveRoomHarness();
        var big = Water("bw_water_1", 2, 2);
        await Place(h, big, 2, 2);
        await Place(h, Water("bw_water_1"), 4, 2); // east of the footprint's top row (local 2,0)

        Assert.Equal(1 << 6, State(big));
    }
}
