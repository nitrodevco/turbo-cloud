using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The tile heights the room sends (<c>HeightMap</c>, <c>HeightMapUpdate</c>) as the client's
/// <c>HeightMapMessageParser</c> reads them: -1 is no tile at all, bit 15 (Habbo's
/// <c>room.stacking_blocked_mask_bit</c>) marks a tile nothing may be stacked on, and the bits below
/// it are a height × 256. Habbo's own starter room (hhus, 2026-10-13): a chair on the floor is
/// -32768, a shelf-top item standing at 1.0 is -32512 - a blocked tile shows where its top furni
/// stands, not that furni's top.
/// </summary>
public sealed class StackHeightEncodingTests
{
    private const int STACKING_BLOCKED = 1 << 15;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void A_chair_on_the_floor_blocks_stacking_at_floor_height_as_on_habbo()
    {
        AddFurni(1, 3, 3, canStack: false);

        Encoded(3, 3)
            .Should()
            .Be(unchecked((short)STACKING_BLOCKED), "hhus sends -32768 for a chair at z 0");
    }

    [Fact]
    public void A_blocking_furni_standing_on_another_shows_the_height_it_stands_at()
    {
        AddFurni(1, 3, 3, canStack: false, z: 1.0);

        Encoded(3, 3)
            .Should()
            .Be(unchecked((short)(STACKING_BLOCKED | 256)), "hhus sends -32512 for z 1");
    }

    [Fact]
    public void A_tile_under_stackable_furniture_is_its_height_alone()
    {
        AddFurni(1, 3, 3, canStack: true);

        Encoded(3, 3).Should().Be((short)256);
    }

    [Fact]
    public void Open_floor_is_its_height_and_a_tile_outside_the_room_is_minus_one()
    {
        Encoded(1, 1).Should().Be((short)0);

        var model = RoomHarness.GetMember(_room.Harness.State, "Model")!;
        var flags = (RoomTileFlags[])RoomHarness.GetMember(model, "BaseFlags")!;

        flags[(5 * 8) + 5] = RoomTileFlags.Disabled | RoomTileFlags.StackBlocked;
        _room.Map.ComputeTile(5, 5);

        Encoded(5, 5).Should().Be((short)-1);
    }

    private short Encoded(int x, int y) =>
        ((short[])RoomHarness.GetMember(_room.Harness.State, "TileEncodedHeights")!)[(y * 8) + x];

    /// <summary>A plain 1x1 floor furni, 1 high, standing at <paramref name="z"/> (the floor by default).</summary>
    private RoomFloorItem AddFurni(
        int objectId,
        int x,
        int y,
        bool canStack,
        double height = 1.0,
        double z = 0.0
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = objectId,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + objectId,
                SpriteId = 1000 + objectId,
                Name = "default_floor",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
                TotalStates = 1,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromValue(height),
                CanStack = canStack,
                CanWalk = false,
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
        item.SetPositionZ(Altitude.FromValue(z));
        item.SetRotation(Rotation.North);
        item.SetLogic(
            new FurnitureFloorLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Add(
            (RoomObjectId)objectId,
            item
        );
        typeof(RoomMapModule)
            .GetMethod("AddItem", All, [typeof(IRoomItem)])!
            .Invoke(_room.Map, [item]);

        return item;
    }

    [Fact]
    public void A_height_is_truncated_to_256ths_as_habbo_does()
    {
        // Habbo: a 2.05 high stack is 524 (2.05 x 256 = 524.8), not 525.
        AddFurni(1, 3, 3, canStack: true, height: 2.05);

        Encoded(3, 3).Should().Be((short)524);
    }

    [Fact]
    public async Task A_tile_that_changes_and_changes_back_before_the_flush_is_not_resent()
    {
        // Habbo resends only the tiles a rotation changed; a furni lifted and put down again
        // (as a rotation does to the tile it keeps) left its tile resent by Turbo.
        var item = AddFurni(1, 3, 3, canStack: true);
        ((HashSet<int>)RoomHarness.GetMember(_room.Harness.State, "DirtyHeightTileIds")!).Clear();
        (
            (Dictionary<int, short>)
                RoomHarness.GetMember(_room.Harness.State, "DirtyHeightSentValues")!
        ).Clear();

        _room.Map.RemoveFloorItem(item);
        typeof(RoomMapModule).GetMethod("AddFloorItem", All)!.Invoke(_room.Map, [item]);
        _room.Harness.Fakes.Log.Calls.Clear();

        await (Task)
            typeof(Turbo.Rooms.Grains.RoomGrain)
                .GetMethod("FlushDirtyTilesAsync", All)!
                .Invoke(_room.Harness.Room, [CancellationToken.None])!;

        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<Turbo.Primitives.Rooms.Snapshots.RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<Turbo.Primitives.Messages.Outgoing.Room.Engine.HeightMapUpdateMessageComposer>()
            .Should()
            .BeEmpty();
    }
}
