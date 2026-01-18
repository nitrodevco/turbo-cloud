using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Events;

public sealed record WiredVariableBoxChangedEvent : RoomEvent
{
    public required List<int> BoxIds { get; init; }
}
