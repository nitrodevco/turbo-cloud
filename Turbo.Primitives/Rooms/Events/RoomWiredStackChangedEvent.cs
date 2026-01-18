using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Events;

public sealed record RoomWiredStackChangedEvent : RoomEvent
{
    public required List<int> StackIds { get; init; }
}
