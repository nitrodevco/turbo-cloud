using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events;

public sealed record FloorItemMovedEvent : RoomEvent
{
    public required RoomObjectId ItemId { get; init; }
}
