using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanLayOnVariable(RoomGrain roomGrain)
    : FurnitureFloorVariable(roomGrain)
{
    protected override string VariableName => "@can_lay_on";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 30;
    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForItem(IRoomFloorItem item) =>
        WiredVariableValue.Parse(item.Logic.CanLay() ? 1 : 0);
}
