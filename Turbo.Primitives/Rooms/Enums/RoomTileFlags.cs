using System;

namespace Turbo.Primitives.Rooms.Enums;

[Flags]
public enum RoomTileFlags : ushort
{
    None = 0,
    Disabled = 1 << 0,
    Open = 1 << 1,
    Closed = 1 << 2,
    StackBlocked = 1 << 3,
    Sittable = 1 << 4,
    Layable = 1 << 5,
    AvatarOccupied = 1 << 6,
}

public static class RoomTileFlagsExtensions
{
    public static RoomTileFlags Add(this RoomTileFlags current, RoomTileFlags toAdd) =>
        current | toAdd;

    public static RoomTileFlags Remove(this RoomTileFlags current, RoomTileFlags toRemove) =>
        current & ~toRemove;

    public static bool Has(this RoomTileFlags current, RoomTileFlags toCheck) =>
        (current & toCheck) != 0;
}
