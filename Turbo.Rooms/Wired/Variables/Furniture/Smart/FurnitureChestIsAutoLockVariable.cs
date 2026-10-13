using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~chest.is_auto_lock</c> (hhus, WIN63-202610121709): held while the chest locks itself. No value, read only.</summary>
public sealed class FurnitureChestIsAutoLockVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureWiredChestLogic>(roomGrain)
{
    protected override string VariableName => "~chest.is_auto_lock";

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool Holds(FurnitureWiredChestLogic logic) => logic.AutoLocks;

    protected override WiredVariableValue GetValueForLogic(FurnitureWiredChestLogic logic) =>
        WiredVariableValue.Default;
}
