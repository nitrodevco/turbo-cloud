using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Avatar;

public abstract record AvatarEvent : RoomEvent
{
    public required RoomObjectId AvatarId { get; init; }
}
