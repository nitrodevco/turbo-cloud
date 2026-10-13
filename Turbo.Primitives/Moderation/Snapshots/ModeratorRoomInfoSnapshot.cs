using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>What the room tool shows (<c>RoomModerationData</c>, <c>RoomToolCtrl.populate</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorRoomInfoSnapshot
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required int UserCount { get; init; }

    [Id(2)]
    public required bool OwnerInRoom { get; init; }

    [Id(3)]
    public required int OwnerId { get; init; }

    [Id(4)]
    public required string OwnerName { get; init; }

    /// <summary>False for a room that no longer exists: the rest is then not sent.</summary>
    [Id(5)]
    public required bool Exists { get; init; }

    [Id(6)]
    public required string Name { get; init; }

    [Id(7)]
    public required string Description { get; init; }

    [Id(8)]
    public required ImmutableArray<string> Tags { get; init; }
}
