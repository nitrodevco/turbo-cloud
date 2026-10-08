using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// A smart furni variable: held by every furni whose logic is <typeparamref name="TLogic"/>, and
/// listed only while one of them is in the room. Smart variables are listed above the internal
/// ones of their band, as the official client's Creator Tools list <c>~clock.*</c> above
/// <c>@id</c>.
/// </summary>
public abstract class FurnitureSmartVariable<TLogic>(RoomGrain roomGrain)
    : FurnitureValueVariable<IRoomItem>(roomGrain),
        IWiredSmartVariable
    where TLogic : class
{
    protected override WiredVariableType VariableType => WiredVariableType.Smart;

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Smart;

    public bool IsPresent() => FurniModule.Items.Any(item => item.Logic is TLogic);

    protected override WiredVariableValue GetValueForItem(IRoomItem item) =>
        GetValueForLogic((TLogic)item.Logic);

    protected abstract WiredVariableValue GetValueForLogic(TLogic logic);

    protected override bool TryGetItemForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out IRoomItem? item
    ) => base.TryGetItemForKey(key, out item) && item.Logic is TLogic;

    /// <summary>The logic of the furni the key names, when it is one of this variable's kind.</summary>
    protected bool TryGetLogicForKey(in WiredVariableKey key, [NotNullWhen(true)] out TLogic? logic)
    {
        logic = null;

        if (!CanBind(key) || !TryGetItemForKey(key, out var item))
            return false;

        logic = (TLogic)item.Logic;

        return true;
    }
}
