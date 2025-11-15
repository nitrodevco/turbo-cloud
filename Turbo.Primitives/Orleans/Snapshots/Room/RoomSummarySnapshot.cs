using System;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomSummarySnapshot
{
    [Id(0)]
    public required long RoomId { get; init; } = -1;

    [Id(1)]
    public required int Population { get; init; } = 0;

    [Id(2)]
    public required string Name { get; init; } = string.Empty;

    [Id(3)]
    public required string Description { get; init; } = string.Empty;

    [Id(4)]
    public required long OwnerId { get; init; } = -1;

    [Id(5)]
    public required string OwnerName { get; init; } = string.Empty;

    [Id(6)]
    public required DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;
}
