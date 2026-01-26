using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanSitOnVariable(RoomGrain roomGrain)
    : FurnitureFloorVariable(roomGrain)
{
    protected override string VariableName => "@can_sit_on";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 40;
    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForItem(IRoomFloorItem item) =>
        WiredVariableValue.Parse(item.Logic.CanSit() ? 1 : 0);
}
