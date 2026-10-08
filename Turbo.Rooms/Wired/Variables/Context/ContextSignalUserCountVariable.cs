using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// How many users the signal that started the stack forwarded.
/// </summary>
public sealed class ContextSignalUserCountVariable(RoomGrain roomGrain) : ContextVariable(roomGrain)
{
    protected override string VariableName => "@signal_user_count";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // sirjonasxx's overview (variables-info #9) lists the context variables in this order.
    protected override ushort Order => 7;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Signal.SelectedAvatarIds.Count;
}
