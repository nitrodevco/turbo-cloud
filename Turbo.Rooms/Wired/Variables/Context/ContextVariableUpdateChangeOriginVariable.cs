using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.variable_update.change_origin</c>: where the change came from, for a "Variable Changed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextVariableUpdateChangeOriginVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.variable_update.change_origin";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 24;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        new()
        {
            [0] = "In Room",
            [1] = "Another Room",
            [2] = "Inspection",
            [3] = "External",
        };

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredVariableChangedEvent e ? (int)e.Origin : 0;
}
