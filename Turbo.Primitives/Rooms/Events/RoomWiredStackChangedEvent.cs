namespace Turbo.Primitives.Rooms.Events;

public sealed record RoomWiredStackChangedEvent : RoomEvent
{
    public required int[] StackIds { get; init; }
}
