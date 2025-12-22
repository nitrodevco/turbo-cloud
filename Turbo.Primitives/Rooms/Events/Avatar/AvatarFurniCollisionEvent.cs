using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Avatar;

public sealed record AvatarFurniCollisionEvent : AvatarEvent
{
    public required RoomObjectId FurniId { get; init; }
}
