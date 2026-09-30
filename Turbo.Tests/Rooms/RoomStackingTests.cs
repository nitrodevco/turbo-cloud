using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Room behaviour without Orleans: <see cref="RoomHarness"/> builds a room grain with real
/// modules over a flat 8x8 model, so tests drive the modules the grain's methods call.
/// </summary>
public class RoomStackingTests
{
    [Fact]
    public void ItemOnAnother_RaisesTheTileToTheTopOfTheStack()
    {
        var room = new RoomHarness();
        room.AddToRoom(
            room.CreateFloorItem(1, 2, 2, Altitude.Zero, stackHeight: Altitude.FromValue(1.0))
        );
        room.AddToRoom(
            room.CreateFloorItem(
                2,
                2,
                2,
                Altitude.FromValue(1.0),
                stackHeight: Altitude.FromValue(0.5)
            )
        );

        Assert.Equal(1.5, room.TileHeight(2, 2).Value, 3);
    }

    [Fact]
    public void EmptyTile_KeepsTheModelHeight()
    {
        var room = new RoomHarness();

        Assert.Equal(0.0, room.TileHeight(5, 5).Value, 3);
    }
}
