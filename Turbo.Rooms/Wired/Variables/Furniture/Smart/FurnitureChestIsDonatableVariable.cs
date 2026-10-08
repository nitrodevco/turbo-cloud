using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~chest.is_donatable</c> (Wired Faculty, 18/03/2026): 1 when the chest's owner lets everyone donate to it. Read only.</summary>
public sealed class FurnitureChestIsDonatableVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureWiredChestLogic>(roomGrain)
{
    protected override string VariableName => "~chest.is_donatable";

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForLogic(FurnitureWiredChestLogic logic) =>
        logic.EveryoneCanDonate ? 1 : 0;
}
