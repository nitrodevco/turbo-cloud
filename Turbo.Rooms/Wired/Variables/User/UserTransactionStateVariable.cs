using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the player is still adding items to their open contract transaction or accepted it.</summary>
public sealed class UserTransactionStateVariable(RoomGrain roomGrain)
    : UserTransactionVariable(roomGrain)
{
    protected override string VariableName => "@transaction.state";

    protected override ushort Order => 203;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        new()
        {
            [(int)WiredTransactionStateType.AddingItems] = "Adding Items",
            [(int)WiredTransactionStateType.Confirmed] = "Confirmed",
        };

    protected override bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    )
    {
        value = (int)progress.State;

        return true;
    }
}
