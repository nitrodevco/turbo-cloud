using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// <c>@occupation</c> (official, October 2026; Wired Faculty tutorial "Info on @position and
/// @occupation"): x, y and rotation packed into one value, <c>(x &lt;&lt; 16) | (y &lt;&lt; 8) |
/// rotation</c>, which is <c>(@position &lt;&lt; 8) | rotation</c>. Writing it moves and turns the
/// furni in one move.
/// </summary>
public sealed class FurnitureOccupationVariable(RoomGrain roomGrain)
    : FurniturePlacementVariable(roomGrain)
{
    protected override string VariableName => "@occupation";
    protected override ushort Order => 45;

    protected override WiredVariableValue GetValueForItem(IRoomItem item) =>
        (item.X << 16) | (item.Y << 8) | (int)item.Rotation;

    /// <summary>A wall item only faces one of two ways, as with <c>@rotation</c>.</summary>
    protected override Placement Apply(IRoomItem item, Placement current, int value)
    {
        var rotation = (Rotation)(value & 0xFF);

        if (!System.Enum.IsDefined(rotation))
            rotation = current.Rotation;
        else if (item is IRoomWallItem && rotation != Rotation.South)
            rotation = Rotation.North;

        return current with
        {
            X = (value >> 16) & 0xFF,
            Y = (value >> 8) & 0xFF,
            Rotation = rotation,
        };
    }
}
