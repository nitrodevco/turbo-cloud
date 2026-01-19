using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanLayOnVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@can_lay_on",
                WiredVariableIdBuilder.WiredVarSubBand.Meta,
                30
            ),
            VariableName = "@can_lay_on",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags = WiredVariableFlags.None,
            TextConnectors = [],
        };

    public override bool CanBind(in WiredVariableBinding binding) =>
        base.CanBind(binding)
        && _roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem)
        && floorItem.Logic.CanLay();

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem))
            return false;

        value = floorItem.Logic.CanLay() ? 1 : 0;

        return true;
    }
}
