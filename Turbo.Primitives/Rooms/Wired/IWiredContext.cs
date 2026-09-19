using System.Collections.Generic;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredContext
{
    public IRoomGrain Room { get; }
    public IWiredPolicy Policy { get; }

    /// <summary>The triggering furni and users.</summary>
    public IWiredSelectionSet Selected { get; }

    /// <summary>What the selectors of the stack picked.</summary>
    public IWiredSelectionSet SelectorPool { get; }

    /// <summary>The furni and users a signal or stack call forwarded into this firing.</summary>
    public IWiredSelectionSet Signal { get; }

    /// <summary>How many stack calls or signals deep this firing is; bounded by config.</summary>
    public int Depth { get; }
    public Dictionary<string, int> Variables { get; }

    /// <summary>
    /// The furni and users a box works on: every input slot of the box resolved against this
    /// firing. Synchronous on purpose; it only reads room state, and conditions are synchronous.
    /// </summary>
    public IWiredSelectionSet GetSelection(IWiredBox wired);
    public WiredContextSnapshot GetSnapshot();
}
