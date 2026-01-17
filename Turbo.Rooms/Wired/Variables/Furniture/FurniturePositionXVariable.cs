using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurniturePositionXVariable(RoomGrain roomGrain)
    : WiredVariable(roomGrain),
        IWiredInternalVariable
{
    public override string VariableName { get; set; } = "@position.x";

    public override WiredVariableTargetType GetVariableTargetType() =>
        WiredVariableTargetType.Furni;

    public override WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Internal;

    public override WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.FurniSource;

    public override WiredVariableFlags GetVariableFlags()
    {
        var flags = base.GetVariableFlags();

        flags = flags.Add(
            WiredVariableFlags.HasValue
                | WiredVariableFlags.CanWriteValue
                | WiredVariableFlags.AlwaysAvailable
        );

        return flags;
    }

    public override bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        if (!_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem))
            return false;

        value = floorItem.X;

        return true;
    }

    public override Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        if (
            !_roomGrain._state.FloorItemsById.TryGetValue(binding.TargetId, out var floorItem)
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
