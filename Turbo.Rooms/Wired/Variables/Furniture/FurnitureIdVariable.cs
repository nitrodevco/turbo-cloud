using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureIdVariable(RoomGrain roomGrain)
    : FurnitureValueVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@id";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;
    protected override ushort Order => 40;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    // A wall item's id is negative, as wired names it everywhere (variables-info #11).
    protected override WiredVariableValue GetValueForItem(IRoomItem item) =>
        WiredFurniIds.ToClient(item);
}
