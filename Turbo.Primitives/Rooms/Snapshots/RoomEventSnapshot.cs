using System;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>A promoted room event (room ad) shown in the room and in the navigator's events tab.</summary>
[GenerateSerializer, Immutable]
public sealed record RoomEventSnapshot
{
    [Id(0)]
    public required int EventId { get; init; }

    [Id(1)]
    public required RoomId RoomId { get; init; }

    [Id(2)]
    public required PlayerId OwnerId { get; init; }

    [Id(3)]
    public required string OwnerName { get; init; }

    [Id(4)]
    public required int CategoryId { get; init; }

    [Id(5)]
    public required string Name { get; init; }

    [Id(6)]
    public required string Description { get; init; }

    [Id(7)]
    public required DateTime CreatedAtUtc { get; init; }

    [Id(8)]
    public required DateTime ExpiresAtUtc { get; init; }

    public bool IsActiveAt(DateTime utcNow) => ExpiresAtUtc > utcNow;

    public int MinutesSinceCreated(DateTime utcNow) =>
        Math.Max(0, (int)(utcNow - CreatedAtUtc).TotalMinutes);

    public int MinutesUntilExpiry(DateTime utcNow) =>
        Math.Max(0, (int)Math.Ceiling((ExpiresAtUtc - utcNow).TotalMinutes));
}
