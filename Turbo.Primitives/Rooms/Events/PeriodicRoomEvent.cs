using Orleans;

namespace Turbo.Primitives.Rooms.Events;

[GenerateSerializer]
public sealed record PeriodicRoomEvent : RoomEvent { }
