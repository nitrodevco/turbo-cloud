using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// How many users the stack's selectors picked. The Wired Faculty tutorial "Ticket queue system"
/// (07/06/2025) shows a player's place in the queue with it: "Place in queue:
/// $(selector_user_count)".
/// </summary>
public sealed class ContextSelectorUserCountVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@selector_user_count";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // After @selector_furni_count (10), as sirjonasxx's overview (variables-info #9) lists them.
    protected override ushort Order => 9;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForSelection(IWiredSelectionSet selectorPool) =>
        selectorPool.SelectedAvatarIds.Count;
}
