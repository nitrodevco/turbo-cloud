using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Wired;

/// <summary>
/// Where the furni movers put a picked wall item. Its wall coordinates move by whole tiles, its
/// offset along the wall and its wall side stay, and its height on screen (the location's second
/// offset) follows the floor it comes to (official client capture 2026-10-24T19-22-52 and
/// 19-24-34, WIN63-202610201032, W1 lab action.33.X11 3/3/2 and action.4.X11 2/2, room model_h):
/// along the wall (coordinates that are no floor tile) both offsets are kept; the first step off
/// the wall onto a floor tile takes 147 off the height and adds 32 per level of that tile (clock
/// 55 to -92 and wardrobe 89 to -58 on level 0, window 70 to -45 on level 1); from one floor tile
/// to another it changes by 32 per level climbed, so the item keeps its altitude in the room
/// (LegacyWallGeometry.as:121-138, getLocation: z = tile height - offset / 32; action.57.B11,
/// level 1 to 0: -26 to -58).
/// </summary>
internal static class WiredWallMoves
{
    private const int OFF_THE_WALL = 147;

    // A floor level is 32 pixels of the location's height offset, half the 64 scale
    // (LegacyWallGeometry.as:130, 135).
    private const int PIXELS_PER_LEVEL = 32;

    // The width of a wall segment in offset pixels: a left wall reads its offset from the far
    // end (LegacyWallGeometry.as:134, scale / 2 - offset).
    private const int SEGMENT_WIDTH = 32;

    /// <summary>
    /// The wall item at <paramref name="start"/> moved by (<paramref name="dx"/>,
    /// <paramref name="dy"/>) tiles. False off the room's grid (inferred: no capture moved one
    /// there, as no floor mover goes off the map). Inferred too: a step from a floor tile back
    /// onto wall coordinates gives back what the step off the wall took.
    /// </summary>
    public static bool TryStep(
        RoomMapModule map,
        WallPosition start,
        int dx,
        int dy,
        out WallPosition to
    )
    {
        var (x, y) = (start.X + dx, start.Y + dy);

        to = start;

        if (!map.InBounds(x, y))
            return false;

        var height = (int)start.Z.Value;
        var fromFloor = IsFloor(map, start.X, start.Y);

        height = (fromFloor, IsFloor(map, x, y)) switch
        {
            (false, false) => height,
            (false, true) => height - OFF_THE_WALL + PIXELS_PER_LEVEL * map.GetFloorLevel(x, y),
            (true, true) => height
                + PIXELS_PER_LEVEL
                    * (map.GetFloorLevel(x, y) - map.GetFloorLevel(start.X, start.Y)),
            (true, false) => height
                + OFF_THE_WALL
                - PIXELS_PER_LEVEL * map.GetFloorLevel(start.X, start.Y),
        };

        to = start with { X = x, Y = y, Z = height };

        return true;
    }

    /// <summary>
    /// The wall item turned onto the other wall side ("r" to "l" and back), its offset along the
    /// wall mirrored: Move and Rotate with a rotation turns it every firing, offset 0 to 32 and
    /// back (official client capture 2026-10-24T17-49-51, W1 lab action.4.B11, 2/2). Inferred:
    /// the mirror of any other offset; every rotation option turns it, a wall having two sides.
    /// </summary>
    public static WallPosition Turned(WallPosition at) =>
        at with
        {
            Rotation = at.Rotation == Rotation.South ? Rotation.West : Rotation.South,
            WallOffset = SEGMENT_WIDTH - at.WallOffset,
        };

    /// <summary>Where a wall item is.</summary>
    public static WallPosition At(IRoomWallItem item) =>
        new(item.X, item.Y, item.WallOffset, item.Z, item.Rotation);

    private static bool IsFloor(RoomMapModule map, int x, int y) =>
        map.InBounds(x, y) && !map.IsTileDisabled(map.ToIdx(x, y));
}
