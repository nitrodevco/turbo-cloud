using Turbo.Primitives.Events;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Events;

/// <summary>
/// Raised by the room directory when what it knows of a room changes: the room loaded, unloaded
/// or changed its listing (name, settings, owner), or a player entered or left it, in which case
/// <see cref="PlayerId"/> names them. Published without being awaited, so a handler may call the
/// directory back.
/// </summary>
public sealed record RoomActivityChangedEvent : IEvent
{
    public required RoomId RoomId { get; init; }

    public PlayerId? PlayerId { get; init; }
}
