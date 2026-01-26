using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanStandOnVariable(RoomGrain roomGrain)
    : FurnitureFloorVariable(roomGrain)
{
    protected override string VariableName => "@can_stand_on";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 50;
    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool TryGetValueForItem(IRoomFloorItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return item.Logic.CanWalk();
    }
}
