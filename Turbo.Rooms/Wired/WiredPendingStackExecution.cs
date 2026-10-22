using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Storage;

namespace Turbo.Rooms.Wired;

internal sealed class WiredPendingStackExecution
{
    public required IWiredStack Stack { get; init; }
    public required List<IWiredAction> Actions { get; init; }
    public IWiredTrigger? Trigger { get; init; }

    /// <summary>The event that started the firing, which the context variables read.</summary>
    public Turbo.Primitives.Rooms.Events.RoomEvent? Event { get; init; }
    public required IWiredPolicy Policy { get; init; }
    public required IWiredSelectionSet Selected { get; init; }
    public required IWiredSelectionSet SelectorPool { get; init; }
    public required IWiredSelectionSet Signal { get; init; }
    public required IWiredSelectionSet EventTargets { get; init; }
    public Dictionary<string, string> CarriedPlaceholders { get; init; } = [];
    public required int Depth { get; init; }
    public long Version { get; set; }
    public long DueAtMs { get; set; }
    public int NextActionIndex { get; set; }
    public int? WaitingActionIndex { get; set; }

    /// <summary>The variable changes held back while the actions run; null with "Execute In Order".</summary>
    public WiredVariableChangeBatch? VariableChanges { get; init; }

    /// <summary>The firing's context variable values, which its actions read and write.</summary>
    public required KeyValueStore ContextValues { get; init; }
}
