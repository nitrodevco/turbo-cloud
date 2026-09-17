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

    public void Cancel() => IsCancelled = true;
}
