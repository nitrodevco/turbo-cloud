using System;
using System.Linq;

namespace Turbo.Primitives.Rooms.Enums;

[Flags]
public enum RoomTileFlags : ushort
{
    None = 0,
    Disabled = 1 << 0,
    Open = 1 << 1,
    Closed = 1 << 2,
    StackBlocked = 1 << 3,
    Walkable = 1 << 4,
    Sittable = 1 << 5,
    Layable = 1 << 6,
    AvatarOccupied = 1 << 7,
    FurnitureOccupied = 1 << 8,
    FurnitureWithRollersOccupied = 1 << 9,
    TileClickListener = 1 << 10,
}

public static class RoomTileFlagsExtensions
{
    public static RoomTileFlags Add(this RoomTileFlags current, RoomTileFlags toAdd) =>
        current | toAdd;

    public static RoomTileFlags Remove(this RoomTileFlags current, RoomTileFlags toRemove) =>
        current & ~toRemove;

    public static bool Has(this RoomTileFlags current, RoomTileFlags toCheck) =>
        (current & toCheck) != 0;

    public static bool Has(this RoomTileFlags current, params RoomTileFlags[] toCheck) =>
        toCheck.Any(flag => (current & flag) != 0);
}
