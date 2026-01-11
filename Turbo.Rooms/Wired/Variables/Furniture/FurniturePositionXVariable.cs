using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurniturePositionXVariable : WiredVariableComputed
{
    public override WiredVariableDefinition Definition =>
        new()
        {
            Key = "position.x",
            Name = "x",
            Target = WiredVariableTargetType.Furni,
            ValueKind = WiredValueType.Int,
            AvailabilityType = WiredAvailabilityType.Persistent,
            InputSourceType = WiredInputSourceType.FurniSource,
            Flags = WiredVariableFlags.CanWriteValue | WiredVariableFlags.AlwaysAvailable,
        };

    public bool CanBind(in WiredVariableBinding binding) => binding.TargetId is not null;

    public WiredValue Get(in WiredVariableBinding binding, WiredExecutionContext ctx)
    {
        if (
            !ctx.Room._liveState.FloorItemsById.TryGetValue(
                binding.TargetId!.Value,
                out var floorItem
            )
        )
            return WiredValue.None();

        return WiredValue.Int(floorItem.X);
    }

    public bool TrySet(in WiredVariableBinding binding, WiredExecutionContext ctx, WiredValue value)
    {
        if (
            !ctx.Room._liveState.FloorItemsById.TryGetValue(
                binding.TargetId!.Value,
                out var floorItem
            )
        )
            return false;

        var newX = value.AsInt();

        if (
            !ctx.Room._furniModule.ValidateFloorItemPlacement(
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
            ctx.Room._mapModule.ToIdx(newX, floorItem.Y),
            floorItem.Rotation
        );

        return true;
    }
}
