using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomPendingSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; } = -1;

    [Id(1)]
    public required RoomEntryState State { get; init; } = RoomEntryState.None;
}
