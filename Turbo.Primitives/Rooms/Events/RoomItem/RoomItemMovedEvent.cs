using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

[GenerateSerializer]
public sealed record RoomItemMovedEvent : RoomItemEvent
{
    [Id(0)]
    public required int PrevIdx { get; init; }
}
