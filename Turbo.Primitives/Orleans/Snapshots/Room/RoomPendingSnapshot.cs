using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomPendingSnapshot
{
    [Id(0)]
    public required int RoomId { get; init; } = -1;

    [Id(1)]
    public required bool Approved { get; init; } = false;
}
