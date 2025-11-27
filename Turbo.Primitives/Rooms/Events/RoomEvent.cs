using Turbo.Primitives.Actor;

namespace Turbo.Primitives.Rooms.Events;

public abstract record RoomEvent
{
    public required long RoomId { get; init; }

    public required ActionContext CausedBy { get; init; }
}
