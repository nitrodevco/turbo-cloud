using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the player has a contract transaction open.</summary>
public sealed class UserTransactionInTradeVariable(RoomGrain roomGrain)
    : UserTransactionVariable(roomGrain)
{
    protected override string VariableName => "@transaction.in_trade";

    protected override ushort Order => 200;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.HeldFlag;

        return true;
    }
}
