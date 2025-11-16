using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomActiveSnapshot : RoomSummarySnapshot { }
