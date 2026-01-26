using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureDimensionsXVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@dimensions.x";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 20;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = item.Definition.Width;

        return true;
    }
}
