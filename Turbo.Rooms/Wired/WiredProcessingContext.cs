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
}
