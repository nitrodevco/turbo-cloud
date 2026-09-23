using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// A user variable every avatar it binds to holds, so reading it cannot fail. Which avatars
/// those are is the type argument's job: <c>@pet_id</c> asks for an <see cref="IRoomPet"/> and
/// so is absent on everything else, because
/// <see cref="UserVariable{TAvatar}.TryGetAvatarForKey"/> never finds one.
/// </summary>
public abstract class UserValueVariable<TAvatar>(RoomGrain roomGrain)
    : UserVariable<TAvatar>(roomGrain)
    where TAvatar : IRoomAvatar
{
    protected sealed override bool TryGetValueForAvatar(
        TAvatar avatar,
        out WiredVariableValue value
    )
    {
        value = GetValueForAvatar(avatar);

        return true;
    }

    protected abstract WiredVariableValue GetValueForAvatar(TAvatar avatar);
}
