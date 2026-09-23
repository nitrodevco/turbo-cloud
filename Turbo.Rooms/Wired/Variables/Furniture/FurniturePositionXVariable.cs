using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurniturePositionXVariable(RoomGrain roomGrain)
    : FurniturePlacementVariable(roomGrain)
{
    protected override string VariableName => "@position.x";
    protected override ushort Order => 40;

    protected override WiredVariableValue GetValueForItem(IRoomItem item) => item.X;

    protected override Placement Apply(IRoomItem item, Placement current, int value) =>
        current with
        {
            X = value,
        };
}
