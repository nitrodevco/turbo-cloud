namespace Turbo.Primitives.Rooms.Events;

public sealed record FloorItemMovedEvent : RoomEvent
{
    public required long ItemId { get; init; }
}
