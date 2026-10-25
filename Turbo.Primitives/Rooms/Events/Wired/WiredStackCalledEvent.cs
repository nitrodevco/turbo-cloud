using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Object;

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
    public required HashSet<RoomObjectId> AvatarIds { get; init; }

    [Id(3)]
    public required int Depth { get; init; }

    [Id(4)]
    public required bool IsNegative { get; init; }

    /// <summary>
    /// What is left of a delayed call's wait: the call is made when its stack fires, and the
    /// called stack's effects wait this long before their own delays.
    /// </summary>
    [Id(5)]
    public long DelayMs { get; init; }
}
