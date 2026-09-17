using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Avatar;

[GenerateSerializer]
public sealed record AvatarWalkOffFurniEvent : AvatarEvent
{
    [Id(0)]
    public required RoomObjectId FurniId { get; init; }
}
