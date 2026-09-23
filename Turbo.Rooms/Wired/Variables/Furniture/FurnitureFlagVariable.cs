using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// A furni variable an item either holds or does not, with nothing to read beyond that. The
/// client asks who holds one, so an item the flag is false for has to answer that it holds
/// nothing: reporting a zero instead puts every furni in the room on that list and leaves the
/// flag saying nothing at all. <see cref="User.UserFlagVariable{TAvatar}"/> is the same thing
/// for an avatar.
/// </summary>
public abstract class FurnitureFlagVariable<TItem>(RoomGrain roomGrain)
    : FurnitureVariable<TItem>(roomGrain)
    where TItem : IRoomItem
{
    protected sealed override bool TryGetValueForItem(TItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return HasFlag(item);
    }

    /// <summary>Whether the item holds the flag right now.</summary>
    protected abstract bool HasFlag(TItem item);
}
