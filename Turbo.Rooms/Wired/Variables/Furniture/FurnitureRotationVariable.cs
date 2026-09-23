using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureRotationVariable(RoomGrain roomGrain)
    : FurniturePlacementVariable(roomGrain)
{
    protected override string VariableName => "@rotation";
    protected override ushort Order => 20;

    protected override WiredVariableValue GetValueForItem(IRoomItem item) => (int)item.Rotation;

    /// <summary>A wall item only faces one of two ways; anything but south reads as north.</summary>
    protected override Placement Apply(IRoomItem item, Placement current, int value)
    {
        var rotation = (Rotation)value;

        if (item is IRoomWallItem && rotation != Rotation.South)
            rotation = Rotation.North;

        return current with
        {
            Rotation = rotation,
        };
    }
}
