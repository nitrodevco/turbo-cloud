using Orleans;

namespace Turbo.Primitives.Orleans.Events.Rooms;

[GenerateSerializer]
public abstract record RoomEvent
{
    [Id(0)]
    public required long RoomId { get; init; }
}
