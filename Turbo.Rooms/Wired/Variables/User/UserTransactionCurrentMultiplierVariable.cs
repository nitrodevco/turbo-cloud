using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>How many times over what the player offers meets the contract, at most its multiplier.</summary>
public sealed class UserTransactionCurrentMultiplierVariable(RoomGrain roomGrain)
    : UserTransactionVariable(roomGrain)
{
    protected override string VariableName => "@transaction.current_multiplier";

    protected override ushort Order => 205;

    protected override bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    )
    {
        value = progress.Multiplier;

        return true;
    }
}
