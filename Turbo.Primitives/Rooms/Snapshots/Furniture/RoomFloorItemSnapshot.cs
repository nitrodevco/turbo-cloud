using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

[GenerateSerializer, Immutable]
public sealed record RoomFloorItemSnapshot : RoomItemSnapshot { }
