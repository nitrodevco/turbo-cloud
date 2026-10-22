using System.Collections.Generic;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

public sealed class WiredProcessingContext(RoomGrain roomGrain)
    : WiredContext(roomGrain),
        IWiredProcessingContext
{
    public required RoomEvent Event { get; init; }
    public required IWiredStack Stack { get; init; }

    /// <summary>Null when the stack was run by a "call stack" action rather than a trigger.</summary>
    public IWiredTrigger? Trigger { get; init; }

    /// <summary>
    /// The furni moves of one random order of the stack's effects, worked out before its
    /// conditions are asked; what "Can Perform Movements" reads. The effects that run if they pass
    /// are put in order again. Null when the stack has no such condition.
    /// </summary>
    internal Dictionary<IWiredAction, WiredMovePlan>? MovePlans { get; set; }
}
