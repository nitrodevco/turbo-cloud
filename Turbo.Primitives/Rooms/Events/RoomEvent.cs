using Turbo.Primitives.Action;

namespace Turbo.Primitives.Rooms.Events;

public abstract record RoomEvent
{
    public required int RoomId { get; init; }

    public required ActionContext? CausedBy { get; init; }
}
