using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

[WiredVariable("furni.position.x")]
public sealed class FurniturePositionXVariable(RoomGrain roomGrain) : WiredVariable(roomGrain)
{
    public override IWiredVariableDefinition Definition =>
        new WiredVariableDefinition()
        {
            Key = "position.x",
            Name = "x",
            Target = WiredVariableTargetType.Furni,
            AvailabilityType = WiredAvailabilityType.Persistent,
            InputSourceType = WiredInputSourceType.FurniSource,
            Flags = WiredVariableFlags.CanWriteValue | WiredVariableFlags.AlwaysAvailable,
        };

    public bool CanBind(in IWiredVariableBinding binding) => binding.TargetId is not null;

    public WiredValue Get(in IWiredVariableBinding binding, WiredExecutionContext ctx)
    {
        if (!ctx.Room._state.FloorItemsById.TryGetValue(binding.TargetId!.Value, out var floorItem))
            return WiredValue.None();

        return WiredValue.Int(floorItem.X);
    }

    public bool TrySet(
        in IWiredVariableBinding binding,
        WiredExecutionContext ctx,
        WiredValue value
    )
    {
        if (!ctx.Room._state.FloorItemsById.TryGetValue(binding.TargetId!.Value, out var floorItem))
            return false;

        var newX = value.AsInt();

        if (
            !ctx.Room.FurniModule.ValidateFloorItemPlacement(
                ctx.AsActionContext(),
                floorItem.ObjectId.Value,
                newX,
                floorItem.Y,
                floorItem.Rotation
            )
        )
            return false;

        ctx.AddFloorItemMovement(
            floorItem,
            ctx.Room.MapModule.ToIdx(newX, floorItem.Y),
            floorItem.Rotation
        );

        return true;
    }
}
