using System.Diagnostics.CodeAnalysis;
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
/// <remarks>
/// The shape is <see cref="Furniture.FurnitureVariable{TItem}"/>'s on purpose: answering false
/// is how either side says its target holds nothing at all, which is what the client draws as
/// not held. Derive from <see cref="UserValueVariable{TAvatar}"/> when every avatar that can
/// bind holds a value, and from <see cref="UserFlagVariable{TAvatar}"/> when the variable is
/// held or not and carries nothing.
/// </remarks>
public abstract class UserVariable<TAvatar>(RoomGrain roomGrain) : WiredInternalVariable(roomGrain)
    where TAvatar : IRoomAvatar
{
    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!CanBind(key) || !TryGetAvatarForKey(key, out var avatar))
            return false;

        return TryGetValueForAvatar(avatar, out value);
    }

    protected abstract bool TryGetValueForAvatar(TAvatar avatar, out WiredVariableValue value);

    /// <summary>
    /// The avatar a key is about, by its room index. That index is how the client addresses
    /// any avatar and the only id all three kinds share: a pet and a bot have no player id,
    /// and a player's own id is a separate thing the <c>@user_id</c> variable reports, just as
    /// <c>@pet_id</c> and <c>@bot_id</c> report theirs. The out parameter is annotated, so a
    /// caller that checks the result does not repeat a null check.
    /// </summary>
    protected virtual bool TryGetAvatarForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out TAvatar? avatar
    )
    {
        avatar = default;

        if (
            !_roomGrain.AvatarModule.TryGetAvatar(key.TargetId, out var found)
            || found is not TAvatar typed
        )
            return false;

        avatar = typed;

        return true;
    }
}
