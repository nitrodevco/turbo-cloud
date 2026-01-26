using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

public abstract class UserVariable<TAvatar>(RoomGrain roomGrain) : WiredInternalVariable(roomGrain)
    where TAvatar : IRoomAvatar
{
    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!CanBind(key) || !TryGetAvatarForKey(key, out var avatar) || avatar is null)
            return false;

        value = GetValueForAvatar(avatar);

        return true;
    }

    protected abstract WiredVariableValue GetValueForAvatar(TAvatar avatar);

    protected virtual bool TryGetAvatarForKey(in WiredVariableKey key, out TAvatar? avatar)
    {
        avatar = default;

        if (
            !_roomGrain._state.AvatarsByPlayerId.TryGetValue(key.TargetId, out var found)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(found, out var avatarObj)
            || avatarObj is not TAvatar typed
        )
            return false;

        avatar = typed;

        return true;
    }
}
