using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomActiveSnapshot : RoomSummarySnapshot { }
