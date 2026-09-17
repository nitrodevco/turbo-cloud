using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

[GenerateSerializer]
public sealed record RoomItemAttatchedEvent : RoomItemEvent;
