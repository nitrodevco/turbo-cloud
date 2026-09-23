using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// A user variable an avatar either holds or does not, with nothing to read beyond that. The
/// client asks who holds one, so an avatar the flag is false for has to answer that it holds
/// nothing: reporting a zero instead puts every avatar in the room on that list and leaves the
/// flag saying nothing at all.
/// <see cref="Furniture.FurnitureFlagVariable{TItem}"/> is the same thing for a furni.
/// </summary>
public abstract class UserFlagVariable<TAvatar>(RoomGrain roomGrain)
    : UserVariable<TAvatar>(roomGrain)
    where TAvatar : IRoomAvatar
{
    protected sealed override bool TryGetValueForAvatar(
        TAvatar avatar,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Default;

        return HasFlag(avatar);
    }

    /// <summary>Whether the avatar holds the flag right now.</summary>
    protected abstract bool HasFlag(TAvatar avatar);
}
