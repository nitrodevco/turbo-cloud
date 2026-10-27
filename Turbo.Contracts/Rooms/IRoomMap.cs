using System.Collections.Generic;
using Turbo.Primitives.Rooms;

namespace Turbo.Contracts.Rooms;

/// <summary>
/// The floor of a room as it is now, read in the room's turn: its tiles, who may walk where and
/// what stands on each. In memory; a tile off the map is no floor and holds nothing.
/// </summary>
public interface IRoomMap
{
    /// <summary>The floor plan's width in tiles.</summary>
    int Width { get; }

    /// <summary>The floor plan's length in tiles.</summary>
    int Height { get; }

    /// <summary>Whether the tile is part of the room's floor (not a hole in the plan, not off it).</summary>
    bool IsFloor(RoomTilePoint tile);

    /// <summary>
    /// An open floor tile with no furni and nobody on it: where a furni can be dropped without
    /// stacking or blocking anyone.
    /// </summary>
    bool IsFree(RoomTilePoint tile);

    /// <summary>
    /// Whether <paramref name="user"/> may stand on the tile now, by the room's own rules for it
    /// (closed tiles, furni that cannot be stood on, another avatar there).
    /// </summary>
    bool CanStand(RoomUser user, RoomTilePoint tile);

    /// <summary>The floor furni on the tile, lowest first; the last is what an avatar there stands on.</summary>
    IReadOnlyList<RoomFurni> FurniAt(RoomTilePoint tile);
}
