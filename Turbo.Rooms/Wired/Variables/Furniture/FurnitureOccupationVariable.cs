using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
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

    // The official list has it after @position, among the Meta variables, before @type (70).
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 74;

    /// <summary>
    /// The client shows a packed value as its parts (purple, with
    /// <c>wiredmenu.inspection.packed_variable.desc</c>) only for a variable that says it has a
    /// text connector (WIN63-202610121624 <c>Util.getPackedVariableText</c>); the table itself is
    /// empty, because the client builds the text from the name.
    /// </summary>
    protected override WiredVariableFlags Flags => base.Flags | WiredVariableFlags.HasTextConnector;

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
