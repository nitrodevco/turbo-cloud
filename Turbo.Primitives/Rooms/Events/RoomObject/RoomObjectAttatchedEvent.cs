using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomObject;

[GenerateSerializer]
public sealed record RoomObjectAttatchedEvent : RoomObjectEvent;
