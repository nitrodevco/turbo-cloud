using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomPendingSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; } = RoomId.Empty;

    [Id(1)]
    public required bool Approved { get; init; } = false;
}
