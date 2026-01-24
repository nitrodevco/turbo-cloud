using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureCanStandOnVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@can_stand_on",
                WiredVariableIdBuilder.WiredVarSubBand.Meta,
                50
            ),
            VariableName = "@can_stand_on",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags = WiredVariableFlags.None,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (
            !CanBind(binding)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
            || item is not IRoomFloorItem floorItem
            || !floorItem.Logic.CanWalk()
        )
            return false;

        value = 1;

        return true;
    }
}
