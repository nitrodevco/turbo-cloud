using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~chest.is_open</c> (Wired Faculty, 18/03/2026): 1 when the chest's owner lets everyone open it. Read only.</summary>
public sealed class FurnitureChestIsOpenVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureWiredChestLogic>(roomGrain)
{
    protected override string VariableName => "~chest.is_open";

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForLogic(FurnitureWiredChestLogic logic) =>
        logic.EveryoneCanOpen ? 1 : 0;
}
