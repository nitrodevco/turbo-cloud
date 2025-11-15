using System;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomActiveInfoSnapshot
{
    [Id(0)]
    public required long RoomId { get; init; }

    [Id(1)]
    public required int Population { get; init; }

    [Id(2)]
    public required string Name { get; init; } = string.Empty;

    [Id(3)]
    public required string Description { get; init; } = string.Empty;

    [Id(4)]
    public required long OwnerId { get; init; }

    [Id(5)]
    public required string OwnerName { get; init; } = string.Empty;

    [Id(6)]
    public required DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;
}
