namespace Turbo.Primitives.Rooms.Events;

public sealed record WiredVariableBoxChangedEvent : RoomEvent
{
    public required int[] BoxIds { get; init; }
}
