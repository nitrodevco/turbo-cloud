using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// What a tile is once its furni are stacked: how high its top is, its flags and the furni on
/// top. <see cref="RoomMapModule.EvaluateTile"/> builds it, for the room as it stands or for a
/// layout that is only being considered.
/// </summary>
public readonly record struct RoomTileState(
    Altitude Height,
    RoomTileFlags Flags,
    IRoomFloorItem? Highest
);
