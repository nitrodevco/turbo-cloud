using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureDimensionsYVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    protected override void Configure(IWiredVariableDefinition def)
    {
        def.Name = "@dimensions.y";
        def.TargetType = WiredVariableTargetType.Furni;
        def.AvailabilityType = WiredAvailabilityType.Internal;
        def.InputSourceType = WiredInputSourceType.FurniSource;
        def.Flags = WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;
    }

    public override bool CanBind(in IWiredVariableBinding binding) => binding.TargetId is not null;

    public override bool TryGet(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        out int value
    )
    {
        value = 0;

        if (
            !_roomGrain._state.FloorItemsById.TryGetValue(
                binding.TargetId!.Value,
                out var floorItem
            )
        )
            return false;

        value = floorItem.Definition.Length;

        return true;
    }
}
