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

    protected override WiredVariableValue GetValueForItem(IRoomItem item) => (item.X << 8) | item.Y;

    protected override Placement Apply(IRoomItem item, Placement current, int value) =>
        current with
        {
            X = (value >> 8) & 0xFF,
            Y = value & 0xFF,
        };
}
