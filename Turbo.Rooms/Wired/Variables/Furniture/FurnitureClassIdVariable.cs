using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureClassIdVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@class_id";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;
    protected override ushort Order => 30;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = item.Definition.SpriteId;

        return true;
    }
}
