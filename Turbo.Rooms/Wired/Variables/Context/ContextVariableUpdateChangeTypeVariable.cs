using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.variable_update.change_type</c>: whether the variable was created, changed or deleted, for a "Variable Changed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextVariableUpdateChangeTypeVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.variable_update.change_type";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 28;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        new()
        {
            [0] = "Created",
            [1] = "Value changed",
            [2] = "Deleted",
        };

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredVariableChangedEvent e ? (int)e.ChangeType : 0;
}
