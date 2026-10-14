using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The contract of the player's open transaction.</summary>
public sealed class UserTransactionContractIdVariable(RoomGrain roomGrain)
    : UserTransactionVariable(roomGrain)
{
    protected override string VariableName => "@transaction.contract_id";

    protected override ushort Order => 202;

    protected override bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    )
    {
        // The contract furni's id, or the Initiate Transaction box's for a custom contract
        // (inference: no value was captured).
        value = progress.ContractId.Value;

        return true;
    }
}
