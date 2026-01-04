namespace Turbo.Primitives.Rooms.Events;

public sealed record PeriodicRoomEvent : RoomEvent
{
    public static PeriodicRoomEvent Instance { get; } =
        new PeriodicRoomEvent { RoomId = -1, CausedBy = null };
}
