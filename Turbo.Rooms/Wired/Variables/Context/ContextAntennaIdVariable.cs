using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// The <c>@id</c> of the antenna whose signal a "Receive Signal" stack heard; 0 for a stack something else started.
/// </summary>
public sealed class ContextAntennaIdVariable(RoomGrain roomGrain) : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.signal.antenna_id";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // The official client's Creator Tools list the context variables in this order; the gaps
    // leave room for the @event.* ones still to come.
    protected override ushort Order => 60;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredSignalEvent signal
            ? signal
                .AntennaIds.OrderBy(id => id)
                .FirstOrDefault(id =>
                    execution.Trigger is not { } trigger || trigger.GetStuffIds().Contains(id)
                )
            : 0;
}
