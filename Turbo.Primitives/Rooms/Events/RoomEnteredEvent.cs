using Turbo.Primitives.Events;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Events;

/// <summary>
/// A player entered a room: it was sent to them and their avatar is in it. The post-commit
/// counterpart of the room's own <c>PlayerEnterEvent</c>, which room listeners hear on the room's
/// turn; this one is for slow work (a message, a reward, a log) and is raised once per entry,
/// never for one that was refused or sent elsewhere.
/// </summary>
public sealed record RoomEnteredEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }

    public required RoomId RoomId { get; init; }
}
