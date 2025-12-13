using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events;

public abstract record RoomItemEvent : RoomEvent
{
    public required RoomObjectId ItemId { get; init; }
}
