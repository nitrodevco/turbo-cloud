using System;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// A smart variable over one value of a furni's int data: read as it is, written within
/// <see cref="Min"/>..<see cref="Max"/> (a value outside is refused).
/// </summary>
public abstract class FurnitureIndexedSmartVariable<TLogic>(RoomGrain roomGrain)
    : FurnitureSmartVariable<TLogic>(roomGrain)
    where TLogic : class, IIndexedValueLogic
{
    protected abstract int Index { get; }

    protected virtual int Min => 0;

    protected virtual int Max => int.MaxValue;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;

    protected override WiredVariableValue GetValueForLogic(TLogic logic) => logic.ValueAt(Index);

    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetLogicForKey(key, out var logic) || value.Value < Min || value.Value > Max)
            return false;

        await logic.SetValueAtAsync(Index, (int)value.Value);

        return true;
    }
}
