using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Events;

[GenerateSerializer]
public sealed record WiredVariableBoxChangedEvent : RoomEvent
{
    [Id(0)]
    public required List<int> BoxIds { get; init; }
}
