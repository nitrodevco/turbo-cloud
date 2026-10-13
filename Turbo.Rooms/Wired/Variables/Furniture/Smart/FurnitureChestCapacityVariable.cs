using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~chest.capacity</c> (hhus, WIN63-202610121709): How many items or credits the chest takes, as its owner set it within its level. Read only.</summary>
public sealed class FurnitureChestCapacityVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureWiredChestLogic>(roomGrain)
{
    protected override string VariableName => "~chest.capacity";

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForLogic(FurnitureWiredChestLogic logic) =>
        logic.Capacity;
}
