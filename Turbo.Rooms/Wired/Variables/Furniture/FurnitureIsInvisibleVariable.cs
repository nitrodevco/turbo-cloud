using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureIsInvisibleVariable(RoomGrain roomGrain)
    : FurnitureFlagVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@is_invisible";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;
    protected override ushort Order => 80;

    protected override bool HasFlag(IRoomItem item) => item.IsInvisible;
}
