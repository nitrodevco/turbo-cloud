using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureTypeVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@type";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 70;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return true;
    }
}
