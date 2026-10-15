using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>When the player's open contract transaction opened.</summary>
public sealed class UserTransactionStartTimeVariable(RoomGrain roomGrain)
    : UserTransactionVariable(roomGrain)
{
    protected override string VariableName => "@transaction.start_time";

    protected override ushort Order => 201;

    protected override bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    )
    {
        // In Unix milliseconds, as @current_time (inference: no value was captured).
        value = progress.StartedAtMs;

        return true;
    }
}
