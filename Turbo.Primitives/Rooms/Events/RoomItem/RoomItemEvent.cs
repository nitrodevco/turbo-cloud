using Orleans;
using Turbo.Primitives.Rooms.Events.RoomObject;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

[GenerateSerializer]
public abstract record RoomItemEvent : RoomObjectEvent;
