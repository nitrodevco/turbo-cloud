using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// A <c>@transaction.*</c> variable: read off the player's open contract transaction, and held
/// only while they have one. Habbo sends all six as not always available (official client
/// capture, WIN63-202610121709); <c>in_trade</c> and <c>can_accept</c> carry no value.
/// </summary>
public abstract class UserTransactionVariable(RoomGrain roomGrain)
    : UserVariable<IRoomPlayer>(roomGrain)
{
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected sealed override bool TryGetValueForAvatar(
        IRoomPlayer avatar,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Default;

        if (!WiredTransactionSystem.TryGetProgress(avatar.PlayerId, out var progress))
            return false;

        return TryGetValueForProgress(progress, out value);
    }

    protected abstract bool TryGetValueForProgress(
        WiredTransactionProgressSnapshot progress,
        out WiredVariableValue value
    );
}
