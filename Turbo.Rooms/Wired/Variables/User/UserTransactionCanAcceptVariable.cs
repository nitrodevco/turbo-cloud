using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether what the player offers meets the contract, so they may accept it.</summary>
public sealed class UserTransactionCanAcceptVariable(RoomGrain roomGrain)
    : UserTransactionVariable(roomGrain)
{
    protected override string VariableName => "@transaction.can_accept";

    protected override ushort Order => 204;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.HeldFlag;

        return progress.CanAccept;
    }
}
