using System.Collections.Generic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Wired;

internal sealed class WiredPendingStackExecution
{
    public required WiredStack Stack { get; init; }
    public required List<FurnitureWiredActionLogic> Actions { get; init; }
    public required FurnitureWiredTriggerLogic Trigger { get; init; }
    public required Dictionary<string, object?> Variables { get; init; }
    public required WiredPolicy Policy { get; init; }
    public required WiredSelectionSet Selected { get; init; }
    public required WiredSelectionSet SelectorPool { get; init; }

    public long Version { get; set; }
    public long DueAtMs { get; set; }
    public int NextActionIndex { get; set; }
    public int? WaitingActionIndex { get; set; }
}
