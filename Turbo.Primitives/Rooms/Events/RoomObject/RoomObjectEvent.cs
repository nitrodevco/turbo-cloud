using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.RoomObject;

[GenerateSerializer]
public abstract record RoomObjectEvent : RoomEvent
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }
}
