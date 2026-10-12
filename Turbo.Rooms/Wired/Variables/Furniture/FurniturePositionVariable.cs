using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// <c>@position</c> (official, October 2026; Wired Faculty tutorial "Info on @position and
/// @occupation"): x and y packed into one value, <c>(x &lt;&lt; 8) | y</c>. Writing it moves the
/// furni to both at once, where setting <c>@position.x</c> and <c>@position.y</c> one after the
/// other passes through a tile that may be blocked.
/// </summary>
public sealed class FurniturePositionVariable(RoomGrain roomGrain)
    : FurniturePlacementVariable(roomGrain)
{
    protected override string VariableName => "@position";

    // The official list has it after @is_invisible, among the Meta variables, before @type (70).
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 76;

    /// <summary>
    /// The client shows a packed value as its parts (purple, with
    /// <c>wiredmenu.inspection.packed_variable.desc</c>) only for a variable that says it has a
    /// text connector (WIN63-202610121624 <c>Util.getPackedVariableText</c>); the table itself is
    /// empty, because the client builds the text from the name.
    /// </summary>
    protected override WiredVariableFlags Flags => base.Flags | WiredVariableFlags.HasTextConnector;

    protected override WiredVariableValue GetValueForItem(IRoomItem item) => (item.X << 8) | item.Y;

    protected override Placement Apply(IRoomItem item, Placement current, int value) =>
        current with
        {
            X = (value >> 8) & 0xFF,
            Y = value & 0xFF,
        };
}
