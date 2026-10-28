using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>
/// Raised when a player speaks in a room. Published through the global event pipeline before the
/// message is broadcast (handlers may rewrite <see cref="Text"/> or <see cref="Cancel"/> it), and
/// through the room event module afterwards so room listeners such as wired can react to it.
/// </summary>
[GenerateSerializer]
public sealed record PlayerChatEvent : PlayerEvent
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required RoomChatType ChatType { get; init; }

    [Id(2)]
    public required int StyleId { get; init; }

    [Id(3)]
    public required int TrackingId { get; init; }

    [Id(4)]
    public required AvatarGestureType Gesture { get; init; }

    [Id(5)]
    public PlayerId? TargetPlayerId { get; init; }

    [Id(6)]
    public required string Text { get; set; }

    [Id(7)]
    public bool IsCancelled { get; private set; }

    /// <summary>
    /// Whether wired may react to the line: always for talk and shouts; for a whisper, unless the
    /// whisperer opted out of whispering to wired or the hotel turned whisper reading off.
    /// </summary>
    [Id(8)]
    public bool WiredMayRead { get; init; } = true;

    /// <summary>
    /// The line as the player typed it, before the word filter: what wired reads (official client
    /// capture 2026-10-25T03-16-09: a line the hotel filter replaced for the room still fired User
    /// Says Keyword and captured its number). Null when nothing was filtered out.
    /// </summary>
    [Id(9)]
    public string? UnfilteredText { get; init; }

    /// <summary>The name addressed by a whisper, as typed; metadata only, with no change to whisper delivery.</summary>
    [Id(10)]
    public string? RecipientName { get; init; }

    /// <summary>The speaker's tile when this line was spoken, for off-turn listeners.</summary>
    [Id(11)]
    public int? SpeakerX { get; init; }

    [Id(12)]
    public int? SpeakerY { get; init; }

    public void Cancel() => IsCancelled = true;
}
