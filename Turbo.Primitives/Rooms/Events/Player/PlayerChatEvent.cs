using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>
/// Raised when a player speaks in a room. Published through the global event pipeline before the
/// message is broadcast (handlers may rewrite <see cref="Text"/> or <see cref="Cancel"/> it), and
/// through the room event module afterwards so room listeners such as wired can react to it.
/// </summary>
public sealed record PlayerChatEvent : PlayerEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required RoomChatType ChatType { get; init; }
    public required int StyleId { get; init; }
    public required int TrackingId { get; init; }
    public required AvatarGestureType Gesture { get; init; }
    public PlayerId? TargetPlayerId { get; init; }

    public required string Text { get; set; }

    public bool IsCancelled { get; private set; }

    public void Cancel() => IsCancelled = true;
}
