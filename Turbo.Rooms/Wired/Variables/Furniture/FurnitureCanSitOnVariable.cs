using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanSitOnVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@can_sit_on",
                WiredVariableIdBuilder.WiredVarSubBand.Meta,
                40
            ),
            VariableName = "@can_sit_on",
            VariableType = WiredVariableType.Internal,
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags = WiredVariableFlags.None,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = default;

        var snapshot = GetVarSnapshot();

        if (
            (binding.TargetType != snapshot.TargetType)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
            || item is not IRoomFloorItem floorItem
            || !floorItem.Logic.CanSit()
        )
            return false;

        return true;
    }
}
