using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureAltitudeVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@altitude",
                WiredVariableIdBuilder.WiredVarSubBand.Position,
                10
            ),
            VariableName = "@altitude",
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

        value = (int)(floorItem.Z * 100);

        return true;
    }

    public override Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        if (
            !CanBind(binding)
            || !_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem)
            || !_roomGrain.FurniModule.ValidateFloorItemPlacement(
                ctx.AsActionContext(),
                floorItem.ObjectId.Value,
                value,
                floorItem.Y,
                floorItem.Rotation
            )
        )
            return Task.FromResult(false);

        ctx.AddFloorItemMovement(
            floorItem,
            _roomGrain.MapModule.ToIdx(value, floorItem.Y),
            floorItem.Rotation
        );

        return Task.FromResult(true);
    }
}
