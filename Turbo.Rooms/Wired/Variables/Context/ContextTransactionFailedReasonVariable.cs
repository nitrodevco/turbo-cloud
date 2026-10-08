using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.transaction_failed.reason</c>: why the transaction failed, for a "Transaction Failed" stack; 0 for a stack something else started.
/// </summary>
public sealed class ContextTransactionFailedReasonVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.transaction_failed.reason";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 18;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        new()
        {
            [0] = "Cancelled By User",
            [1] = "Invalid Trade",
            [2] = "Timeout",
            [3] = "Cancelled By Wired",
            [4] = "Already Trading",
            [5] = "Wired Misconfiguration",
            [6] = "No Sufficient Funds",
            [7] = "Funds No Longer Available",
            [8] = "User Cant Trade",
            [9] = "Chest Owner Cant Trade",
            [10] = "Empty Transaction",
            [12] = "Feature Disabled",
            [13] = "Chest Not In Room",
            [14] = "Too Many Chests",
            [15] = "No Wired Chests Or Locked",
            [16] = "Can Not Give All To Multiple Users",
            [17] = "Trade Limit Wired",
            [18] = "Rate Limit",
            [19] = "At Capacity",
            [20] = "Misconfig Invalid Multiplier",
            [21] = "Misconfig Too Many Or No Contracts",
            [22] = "Misconfig No Users",
            [23] = "Misconfig Invalid Timeout",
            [24] = "Outdated Client Version",
            [25] = "User Left Room",
            [1000] = "Internal Error",
            [1001] = "Internal Error Db",
            [1002] = "Internal Error Reload Required",
        };

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is WiredTransactionFailedEvent e ? (int)e.Reason : 0;
}
