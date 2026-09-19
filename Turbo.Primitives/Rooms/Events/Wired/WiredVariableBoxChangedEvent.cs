using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Events.Wired;

[GenerateSerializer]
public sealed record WiredVariableBoxChangedEvent : RoomEvent
{
    [Id(0)]
    public required List<int> BoxIds { get; init; }
}
