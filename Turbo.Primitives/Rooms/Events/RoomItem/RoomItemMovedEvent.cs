namespace Turbo.Primitives.Rooms.Events.RoomItem;

public sealed record RoomItemMovedEvent : RoomItemEvent
{
    public required int PrevIdx { get; init; }
}
