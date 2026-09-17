using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Avatar;

[GenerateSerializer]
public sealed record AvatarWalkOnFurniEvent : AvatarEvent
{
    [Id(0)]
    public required RoomObjectId FurniId { get; init; }
}
