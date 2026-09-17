using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Events;

[GenerateSerializer]
public sealed record RoomWiredStackChangedEvent : RoomEvent
{
    [Id(0)]
    public required List<int> StackIds { get; init; }
}
