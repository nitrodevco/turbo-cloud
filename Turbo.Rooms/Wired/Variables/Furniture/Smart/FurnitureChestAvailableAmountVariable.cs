using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~chest.available_amount</c> (Wired Faculty, 18/03/2026): What a wired chest holds: credits in a credit chest, items in a furni chest. Read only.</summary>
public sealed class FurnitureChestAvailableAmountVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureWiredChestLogic>(roomGrain)
{
    protected override string VariableName => "~chest.available_amount";

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForLogic(FurnitureWiredChestLogic logic) =>
        logic.AvailableAmount;
}
