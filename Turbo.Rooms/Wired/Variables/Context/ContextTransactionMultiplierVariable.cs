using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.transaction_complete.multiplier</c>: how many times over the contract was taken, for a "Transaction Completed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextTransactionMultiplierVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.transaction_complete.multiplier";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 23;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredTransactionCompletedEvent e ? e.Multiplier : 0;
}
