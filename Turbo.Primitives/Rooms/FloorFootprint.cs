using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// The tiles a floor item covers: its anchor tile and its definition's width and length, turned
/// the way it stands. Facing east or west swaps the two. This is the one place that knows it;
/// the map, placement, the floor plan check and "is an avatar at this furni" all ask here. Two
/// of the four copies it replaced forgot the swap, so a turned furni answered for the tiles it
/// would have covered unturned.
/// </summary>
public readonly record struct FloorFootprint(int X, int Y, int Width, int Length)
{
    public static FloorFootprint Of(int x, int y, Rotation rotation, int width, int length)
    {
        if (width > 0 && length > 0 && rotation is Rotation.East or Rotation.West)
            (width, length) = (length, width);

        return new(x, y, width, length);
    }

    /// <summary>Where a floor item stands now.</summary>
    public static FloorFootprint Of(IRoomFloorItem item) =>
        Of(item.X, item.Y, item.Rotation, item.Definition.Width, item.Definition.Length);

    /// <summary>Every covered tile, row by row. Whether they lie on a map is the caller's to ask.</summary>
    public IEnumerable<(int X, int Y)> Tiles()
    {
        for (var x = X; x < X + Width; x++)
        {
            for (var y = Y; y < Y + Length; y++)
                yield return (x, y);
        }
    }

    /// <summary>
    /// How many steps a tile is from the nearest covered one, a diagonal step counting as one;
    /// zero on the furni itself.
    /// </summary>
    public int DistanceTo(int x, int y)
    {
        var dx = Math.Max(X - x, Math.Max(0, x - (X + Width - 1)));
        var dy = Math.Max(Y - y, Math.Max(0, y - (Y + Length - 1)));

        return Math.Max(dx, dy);
    }

    /// <summary>A tile on the furni or touching it, diagonals included.</summary>
    public bool IsOnOrNextTo(int x, int y) => DistanceTo(x, y) <= 1;
}
