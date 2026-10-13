using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~chest.locked</c> (hhus, WIN63-202610121709): held while the chest is locked, when wired cannot use it. No value, read only.</summary>
public sealed class FurnitureChestLockedVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureWiredChestLogic>(roomGrain)
{
    protected override string VariableName => "~chest.locked";

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool Holds(FurnitureWiredChestLogic logic) => logic.IsLocked;

    protected override WiredVariableValue GetValueForLogic(FurnitureWiredChestLogic logic) =>
        WiredVariableValue.Default;
}
