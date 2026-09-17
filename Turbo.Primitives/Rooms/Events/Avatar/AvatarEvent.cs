using Orleans;
using Turbo.Primitives.Rooms.Events.RoomObject;

namespace Turbo.Primitives.Rooms.Events.Avatar;

[GenerateSerializer]
public abstract record AvatarEvent : RoomObjectEvent;
