using Turbo.Primitives.Action;
using Turbo.Primitives.Events;

namespace Turbo.Primitives.Rooms.Events;

public abstract record RoomEvent : IEvent
{
    public required RoomId RoomId { get; init; }

    public ActionContext CausedBy { get; init; } = ActionContext.Invalid;
}
