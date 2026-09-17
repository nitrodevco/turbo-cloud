using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

[GenerateSerializer]
public sealed record RoomItemDetachedEvent : RoomItemEvent;
