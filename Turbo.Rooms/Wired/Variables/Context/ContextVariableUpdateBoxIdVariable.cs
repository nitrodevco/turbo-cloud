using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.variable_update.box_id</c>: the variable box whose variable changed, for a "Variable Changed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextVariableUpdateBoxIdVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.variable_update.box_id";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 29;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredVariableChangedEvent e ? e.BoxId : 0;
}
