using System;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The direction from one tile to another under a <see cref="WiredDirectionalSystemType"/>.
/// North is towards smaller y and east towards larger x, as everywhere in a room.
/// </summary>
internal static class WiredDirections
{
    // tan(22.5 degrees): how far off an axis a tile may be and still count as along it.
    private const double DIFFUSE_AXIS_SLOPE = 0.41421356237309503;

    /// <summary>Null when both tiles are the same: there is no direction to turn to.</summary>
    public static Rotation? Resolve(WiredDirectionalSystemType system, int dx, int dy)
    {
        if (dx == 0 && dy == 0)
            return null;

        var (ax, ay) = (Math.Abs(dx), Math.Abs(dy));

        var diagonal = system switch
        {
            WiredDirectionalSystemType.EightStraight => ax != 0 && ay != 0,
            WiredDirectionalSystemType.EightDiffuse => Math.Min(ax, ay)
                > Math.Max(ax, ay) * DIFFUSE_AXIS_SLOPE,
            _ => false,
        };

        if (diagonal)
            return (dx > 0, dy > 0) switch
            {
                (true, false) => Rotation.NorthEast,
                (true, true) => Rotation.SouthEast,
                (false, true) => Rotation.SouthWest,
                _ => Rotation.NorthWest,
            };

        var horizontal =
            system == WiredDirectionalSystemType.FourPreferVertical ? ax > ay : ax >= ay;

        // On an exact diagonal the two eight-way systems never get here, so the tie only
        // matters to the four-way ones.
        if (horizontal)
            return dx > 0 ? Rotation.East : Rotation.West;

        return dy > 0 ? Rotation.South : Rotation.North;
    }
}
