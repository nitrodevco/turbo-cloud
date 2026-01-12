using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

[WiredVariable("furni.position.x")]
public sealed class FurniturePositionXVariable(RoomGrain roomGrain) : WiredVariable(roomGrain)
{
    public override IWiredVariableDefinition VarDefinition =>
        new WiredVariableDefinition()
        {
            Key = "furni.position.x",
            Name = "position.x",
            Target = WiredVariableTargetType.Furni,
            AvailabilityType = WiredAvailabilityType.Persistent,
            InputSourceType = WiredInputSourceType.FurniSource,
            Flags = WiredVariableFlags.CanWriteValue | WiredVariableFlags.AlwaysAvailable,
        };

    public override bool CanBind(in IWiredVariableBinding binding) => binding.TargetId is not null;

    public override bool TryGet(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        out int value
    )
    {
        if (
            !_roomGrain._state.FloorItemsById.TryGetValue(
                binding.TargetId!.Value,
                out var floorItem
            )
        )
        {
            value = 0;

            return false;
        }

        value = floorItem.X;

        return true;
    }

    public override bool SetValue(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        if (
            !_roomGrain._state.FloorItemsById.TryGetValue(
                binding.TargetId!.Value,
                out var floorItem
            )
            || !_roomGrain.FurniModule.ValidateFloorItemPlacement(
                ctx.AsActionContext(),
                floorItem.ObjectId.Value,
                value,
                floorItem.Y,
                floorItem.Rotation
            )
        )
            return false;

        ctx.AddFloorItemMovement(
            floorItem,
            _roomGrain.MapModule.ToIdx(value, floorItem.Y),
            floorItem.Rotation
        );

        return true;
    }

    public override bool RemoveValue(string key)
    {
        return false;
    }
}
