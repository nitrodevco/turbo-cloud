using System.Diagnostics.CodeAnalysis;
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

        if (!CanBind(key) || !TryGetItemForKey(key, out var item))
            return false;

        return TryGetValueForItem(item, out value);
    }

    protected abstract bool TryGetValueForItem(TItem item, out WiredVariableValue value);

    /// <summary>
    /// The item the key names, when it is in the room and of this variable's kind. The out
    /// parameter is annotated, so a caller that checks the result does not repeat a null check —
    /// and one that forgets it does not silently pass null on (the placement variables did).
    /// </summary>
    protected virtual bool TryGetItemForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out TItem? item
    )
    {
        item = default;

        if (
            !_roomGrain.FurniModule.TryGetItem(key.TargetId, out var found)
            || found is not TItem typed
        )
            return false;

        item = typed;

        return true;
    }
}
