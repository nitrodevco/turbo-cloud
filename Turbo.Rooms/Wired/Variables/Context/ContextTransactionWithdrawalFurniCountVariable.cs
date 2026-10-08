using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.transaction_complete.withdrawal.furni_count</c>: the furni the chests gave the player, for a "Transaction Completed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextTransactionWithdrawalFurniCountVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.transaction_complete.withdrawal.furni_count";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 20;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredTransactionCompletedEvent e ? e.WithdrawalFurniCount : 0;
}
