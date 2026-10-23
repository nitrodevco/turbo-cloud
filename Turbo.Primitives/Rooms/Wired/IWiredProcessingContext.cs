using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredProcessingContext : IWiredContext
{
    public RoomEvent Event { get; }
    public IWiredStack Stack { get; }

    /// <summary>Null when the stack was run by a "call stack" action rather than a trigger.</summary>
    public IWiredTrigger? Trigger { get; }

    /// <summary>
    /// The stack is only being asked whether it would pass (a Says Keyword box deciding whether
    /// to hide a line): nothing it decides runs, so an add-on that keeps state leaves it as it is.
    /// </summary>
    public bool IsDryRun => false;
}
