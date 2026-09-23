using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurniturePositionYVariable(RoomGrain roomGrain)
    : FurniturePlacementVariable(roomGrain)
{
    protected override string VariableName => "@position.y";
    protected override ushort Order => 30;

    protected override WiredVariableValue GetValueForItem(IRoomItem item) => item.Y;

    protected override Placement Apply(IRoomItem item, Placement current, int value) =>
        current with
        {
            Y = value,
        };
}
