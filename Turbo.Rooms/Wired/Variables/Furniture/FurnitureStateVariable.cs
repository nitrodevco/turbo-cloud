using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureStateVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@state",
                WiredVariableIdBuilder.WiredVarSubBand.Base,
                10
            ),
            VariableName = "@state",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags =
                WiredVariableFlags.HasValue
                | WiredVariableFlags.CanWriteValue
                | WiredVariableFlags.AlwaysAvailable,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (
            !CanBind(binding)
            || !_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem)
        )
            return false;

        value = floorItem.Logic.StuffData?.GetState() ?? 0;

        return true;
    }

    public override async Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        if (!_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem))
            return false;

        await floorItem.Logic.SetStateAsync(value);

        return true;
    }

    public override bool RemoveValue(string key)
    {
        return false;
    }
}
