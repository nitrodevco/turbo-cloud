using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Wired;

internal sealed class WiredPendingStackExecution
{
    public required WiredStack Stack { get; init; }
    public required IReadOnlyList<FurnitureWiredActionLogic> Actions { get; init; }
    public FurnitureWiredTriggerLogic? Trigger { get; init; } = null;

    public required Dictionary<string, object?> Variables { get; init; }
    public required IWiredSelectionSet Selected { get; init; }
    public required IWiredSelectionSet SelectorPool { get; init; }

    public long Version { get; set; }
    public long DueAtMs { get; set; }
    public EffectModeType EffectMode { get; set; }
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
    public int NextActionIndex { get; set; }
}
