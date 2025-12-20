using System;
using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomPointerSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; } = -1;

    [Id(1)]
    public required DateTime ActiveSinceUtc { get; init; } = DateTime.UtcNow;
}
