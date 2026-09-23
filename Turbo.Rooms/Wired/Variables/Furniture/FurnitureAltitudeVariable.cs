using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureAltitudeVariable(RoomGrain roomGrain)
    : FurniturePlacementVariable(roomGrain)
{
    protected override string VariableName => "@altitude";
    protected override ushort Order => 10;

    protected override WiredVariableValue GetValueForItem(IRoomItem item) => item.Z.ToInt();

    protected override Placement Apply(IRoomItem item, Placement current, int value) =>
        current with
        {
            Z = Altitude.FromInt(value),
        };
}
