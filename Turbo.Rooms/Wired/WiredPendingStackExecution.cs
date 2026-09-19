using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

internal sealed class WiredPendingStackExecution
{
    public required IWiredStack Stack { get; init; }
    public required List<IWiredAction> Actions { get; init; }
    public IWiredTrigger? Trigger { get; init; }
    public required IWiredPolicy Policy { get; init; }
    public required IWiredSelectionSet Selected { get; init; }
    public required IWiredSelectionSet SelectorPool { get; init; }
    public required IWiredSelectionSet Signal { get; init; }
    public required int Depth { get; init; }
    public long Version { get; set; }
    public long DueAtMs { get; set; }
    public int NextActionIndex { get; set; }
    public int? WaitingActionIndex { get; set; }
}
