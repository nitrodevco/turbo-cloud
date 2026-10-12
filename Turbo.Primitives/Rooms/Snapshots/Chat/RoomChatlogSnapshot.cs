using System;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots.Chat;

[GenerateSerializer, Immutable]
public sealed record RoomChatlogSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }

    [Id(2)]
    public PlayerId? TargetPlayerId { get; init; }

    /// <summary>The line as the room heard it, after the word filter.</summary>
    [Id(3)]
    public required string Text { get; init; }

    /// <summary>
    /// When it was said. The line reaches the database seconds later, through two buffers, and
    /// moderation needs the moment it was said.
    /// </summary>
    [Id(4)]
    public required DateTime SaidAt { get; init; }

    /// <summary>What the player typed, when the word filter changed it; null otherwise.</summary>
    [Id(5)]
    public string? Typed { get; init; }
}
