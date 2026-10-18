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
    /// The furni moves the stack's effects will make if its conditions pass, worked out before
    /// they are asked, in the order the effects will run; what "Can Perform Movements" reads.
    /// Null when the stack has no such condition.
    /// </summary>
    internal Dictionary<IWiredAction, WiredMovePlan>? MovePlans { get; set; }
}
