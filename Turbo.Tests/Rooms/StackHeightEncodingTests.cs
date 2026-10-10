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
/// <c>HeightMapMessageParser</c> reads them: -1 is no tile at all, bit 14 marks a tile nothing may be
/// stacked on, and the low bits are its height × 256. A tile under furniture that blocks stacking
/// keeps its height - the client checks a moved furni's new tiles against it - and is not -1, which
/// would tell the client the tile is not part of the room.
/// </summary>
public sealed class StackHeightEncodingTests
{
    private const int STACKING_BLOCKED = 1 << 14;
    private const int HEIGHT_MASK = STACKING_BLOCKED - 1;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void A_tile_under_furniture_that_blocks_stacking_keeps_its_height()
    {
        AddFurni(1, 3, 3, canStack: false);

        var encoded = Encoded(3, 3);

        encoded.Should().BeGreaterThanOrEqualTo((short)0, "the tile is part of the room");
        (encoded & STACKING_BLOCKED).Should().Be(STACKING_BLOCKED, "nothing may be stacked on it");
        ((encoded & HEIGHT_MASK) / 256.0)
            .Should()
            .Be(1.0, "the furni's top is its stacking height");
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

    /// <summary>A plain 1x1 floor furni, 1 high, standing on the floor.</summary>
    private void AddFurni(int objectId, int x, int y, bool canStack)
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
                StackHeight = Altitude.FromInt(100),
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
        item.SetPositionZ(Altitude.Zero);
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
    }
}
