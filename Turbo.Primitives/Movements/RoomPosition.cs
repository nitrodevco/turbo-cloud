using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Where in a room an asset stood or was put, as its furniture row keeps it;
/// <see cref="WallOffset"/> for a wall item only.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct RoomPosition(
    [property: Id(0)] int X,
    [property: Id(1)] int Y,
    [property: Id(2)] double Z,
    [property: Id(3)] Rotation Rotation,
    [property: Id(4)] int? WallOffset
);
