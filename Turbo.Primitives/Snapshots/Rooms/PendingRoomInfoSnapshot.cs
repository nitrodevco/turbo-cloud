using Orleans;

namespace Turbo.Primitives.Snapshots.Rooms;

[GenerateSerializer, Immutable]
public sealed record PendingRoomInfoSnapshot
{
    [Id(0)]
    public required long RoomId { get; init; }

    [Id(1)]
    public required bool Approved { get; init; }
}
