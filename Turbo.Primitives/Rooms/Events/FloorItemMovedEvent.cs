namespace Turbo.Primitives.Rooms.Events;

public sealed record FloorItemMovedEvent(long RoomId, long ItemId) : RoomEvent(RoomId);
