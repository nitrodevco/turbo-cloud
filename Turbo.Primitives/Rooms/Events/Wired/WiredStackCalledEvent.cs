using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A "call another stack" action asked other stacks to run with its selection. Negative calls
/// run the target only when its conditions fail.
/// </summary>
[GenerateSerializer]
public sealed record WiredStackCalledEvent : RoomEvent
{
    [Id(0)]
    public required List<int> StackIds { get; init; }

    [Id(1)]
    public required HashSet<int> FurniIds { get; init; }

    [Id(2)]
    public required HashSet<int> PlayerIds { get; init; }

    [Id(3)]
    public required int Depth { get; init; }

    [Id(4)]
    public required bool IsNegative { get; init; }
}
