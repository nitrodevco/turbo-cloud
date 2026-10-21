using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// A yes/no setting of an area hide furni. Habbo sends these with hasValue off and
/// canCreateAndDelete on (hhus, WIN63-202610121709): the furni holds the variable while the setting is
/// on, giving it turns the setting on and removing it turns it off.
/// </summary>
public abstract class FurnitureAreaHideFlagVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureAreaHideLogic>(roomGrain)
{
    protected abstract int Index { get; }

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.CanCreateAndDelete;

    protected override bool Holds(FurnitureAreaHideLogic logic) => logic.ValueAt(Index) != 0;

    protected override WiredVariableValue GetValueForLogic(FurnitureAreaHideLogic logic) =>
        WiredVariableValue.Default;

    public override async Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        if (!CanBind(key) || !TryGetKindForKey(key, out var item))
            return false;

        var logic = (FurnitureAreaHideLogic)item.Logic;

        if (logic.ValueAt(Index) != 0)
            return false;

        await logic.SetValueAtAsync(Index, 1);

        return true;
    }

    public override bool RemoveValue(WiredVariableKey key)
    {
        if (!CanBind(key) || !TryGetKindForKey(key, out var item))
            return false;

        var logic = (FurnitureAreaHideLogic)item.Logic;

        if (logic.ValueAt(Index) == 0)
            return false;

        logic
            .SetValueAtAsync(Index, 0)
            .LogAndForget(
                Logger,
                "turn off an area hide setting in room {RoomId}",
                _roomGrain.RoomId
            );

        return true;
    }
}
