using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.RoomObject;

public abstract record RoomObjectEvent : RoomEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
