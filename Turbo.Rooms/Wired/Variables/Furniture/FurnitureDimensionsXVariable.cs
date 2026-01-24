using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureDimensionsXVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@dimensions.x",
                WiredVariableIdBuilder.WiredVarSubBand.Meta,
                20
            ),
            VariableName = "@dimensions.x",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags = WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (
            !CanBind(binding)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
        )
            return false;

        value = item.Definition.Width;

        return true;
    }
}
