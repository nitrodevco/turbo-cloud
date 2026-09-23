using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// A furni variable every item it binds to holds, so reading it cannot fail. Which items those
/// are is the type argument's job: <c>@wall_item_offset</c> asks for an
/// <see cref="Primitives.Rooms.Object.Furniture.Wall.IRoomWallItem"/> and so is absent on a
/// floor item, because <see cref="FurnitureVariable{TItem}.TryGetItemForKey"/> never finds one.
/// </summary>
public abstract class FurnitureValueVariable<TItem>(RoomGrain roomGrain)
    : FurnitureVariable<TItem>(roomGrain)
    where TItem : IRoomItem
{
    protected sealed override bool TryGetValueForItem(TItem item, out WiredVariableValue value)
    {
        value = GetValueForItem(item);

        return true;
    }

    protected abstract WiredVariableValue GetValueForItem(TItem item);
}
