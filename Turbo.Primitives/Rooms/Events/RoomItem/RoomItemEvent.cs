using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

public abstract record RoomItemEvent : RoomEvent
{
    public required RoomObjectId FurniId { get; init; }
}
