using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public abstract class FurnitureVariable<TItem>(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain)
    where TItem : IRoomItem
{
    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Furni;

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!CanBind(key) || !TryGetItemForKey(key, out var item) || item is null)
            return false;

        value = GetValueForItem(item);

        return true;
    }

    protected abstract WiredVariableValue GetValueForItem(TItem item);

    protected virtual bool TryGetItemForKey(in WiredVariableKey key, out TItem? item)
    {
        item = default;

        if (
            !_roomGrain._state.ItemsById.TryGetValue(key.TargetId, out var found)
            || found is not TItem typed
        )
            return false;

        item = typed;

        return true;
    }
}
