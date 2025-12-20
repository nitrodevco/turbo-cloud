using System;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public record RoomSummarySnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; } = -1;

    [Id(1)]
    public required string Name { get; init; } = string.Empty;

    [Id(2)]
    public required string Description { get; init; } = string.Empty;

    [Id(3)]
    public required PlayerId OwnerId { get; init; } = -1;

    [Id(4)]
    public required string OwnerName { get; init; } = string.Empty;

    [Id(5)]
    public required int Population { get; init; } = 0;

    [Id(6)]
    public required DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;
}
