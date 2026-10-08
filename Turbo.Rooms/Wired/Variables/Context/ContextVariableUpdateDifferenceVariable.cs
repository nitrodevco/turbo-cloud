using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.variable_update.difference</c>: the new value less the old, for a "Variable Changed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextVariableUpdateDifferenceVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.variable_update.difference";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 25;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredVariableChangedEvent e
            ? WiredVariableValue.Parse(e.Value.Value - e.PreviousValue.Value)
            : 0;
}
