using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// A variable read off an avatar. Pets and bots are avatars too, so a subclass that asks for
/// <see cref="IRoomAvatar"/> answers for all three and one that asks for
/// <see cref="IRoomPlayer"/> answers only where a player makes sense.
/// </summary>
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

    /// <summary>
    /// The avatar a key is about, by its room index. That index is how the client addresses
    /// any avatar and the only id all three kinds share: a pet and a bot have no player id,
    /// and a player's own id is a separate thing the <c>@user_id</c> variable reports, just as
    /// <c>@pet_id</c> and <c>@bot_id</c> report theirs.
    /// </summary>
    protected virtual bool TryGetAvatarForKey(in WiredVariableKey key, out TAvatar? avatar)
    {
        avatar = default;

        if (!_roomGrain.AvatarModule.TryGetAvatar(key.TargetId, out var found))
            return false;

        avatar = found is TAvatar typed ? typed : default;

        return avatar is not null;
    }
}
