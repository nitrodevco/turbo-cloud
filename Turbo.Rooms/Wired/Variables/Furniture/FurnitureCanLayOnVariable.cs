using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanLayOnVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    public override string VariableName { get; set; } = "@can_lay_on";

    public override WiredVariableTargetType GetVariableTargetType() =>
        WiredVariableTargetType.Furni;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Internal;

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.FurniSource;

    public override bool CanBind(in IWiredVariableBinding binding) =>
        base.CanBind(binding)
        && _roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem)
        && floorItem.Logic.CanLay();

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem))
            return false;

        value = floorItem.Logic.CanLay() ? 1 : 0;

        return true;
    }
}
