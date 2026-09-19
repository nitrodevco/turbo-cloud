using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredProcessingContext : IWiredContext
{
    public RoomEvent Event { get; }
    public IWiredStack Stack { get; }

    /// <summary>Null when the stack was run by a "call stack" action rather than a trigger.</summary>
    public IWiredTrigger? Trigger { get; }
}
