using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureIsStackableVariable(RoomGrain roomGrain)
    : FurnitureFlagVariable<IRoomFloorItem>(roomGrain)
{
    protected override string VariableName => "@is_stackable";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 60;

    protected override bool HasFlag(IRoomFloorItem item) => item.Logic.CanStack();
}
