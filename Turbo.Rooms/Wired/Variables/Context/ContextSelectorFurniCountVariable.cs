using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

public sealed class ContextSelectorFurniCountVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternal(
                WiredVariableTargetType.Context,
                "@selector_furni_count"
            ),
            VariableName = "@selector_furni_count",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Context,
            Flags = WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable,
            TextConnectors = [],
        };

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        return true;
    }
}
