using System;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomPointerSnapshot
{
    [Id(0)]
    public required long RoomId { get; init; }

    [Id(1)]
    public required DateTimeOffset Since { get; init; }
}
