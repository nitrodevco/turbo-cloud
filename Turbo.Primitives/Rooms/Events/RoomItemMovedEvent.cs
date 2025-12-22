namespace Turbo.Primitives.Rooms.Events;

public sealed record RoomItemMovedEvent : RoomItemEvent
{
    public required int PrevIdx { get; init; }
}
