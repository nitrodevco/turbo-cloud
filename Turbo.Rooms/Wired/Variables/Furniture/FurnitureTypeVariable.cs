using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureTypeVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    public override string VariableName { get; set; } = "@type";

    public override WiredVariableTargetType GetVariableTargetType() =>
        WiredVariableTargetType.Furni;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Internal;

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.FurniSource;

    public override WiredVariableFlags GetVariableFlags()
    {
        var flags = base.GetVariableFlags();

        flags = flags.Add(WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable);

        return flags;
    }

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem))
            return false;

        value = 0;

        return true;
    }
}
