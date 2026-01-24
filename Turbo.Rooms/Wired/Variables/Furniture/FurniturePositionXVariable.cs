using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurniturePositionXVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@position.x",
                WiredVariableIdBuilder.WiredVarSubBand.Position,
                40
            ),
            VariableName = "@position.x",
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
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
        )
            return false;

        value = item.X;

        return true;
    }

    public override async Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        if (
            !CanBind(binding)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
        )
            return false;

        if (item is IRoomFloorItem floorItem)
        {
            if (
                await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                    ctx.AsActionContext(),
                    floorItem.ObjectId.Value,
                    value,
                    floorItem.Y,
                    floorItem.Rotation
                )
            )
            {
                ctx.AddFloorItemMovement(
                    floorItem,
                    _roomGrain.MapModule.ToIdx(value, floorItem.Y),
                    floorItem.Z,
                    floorItem.Rotation
                );

                return true;
            }
        }
        else if (item is IRoomWallItem wallItem)
        {
            if (
                await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                    ctx.AsActionContext(),
                    wallItem.ObjectId.Value,
                    value,
                    wallItem.Y,
                    wallItem.Z,
                    wallItem.WallOffset,
                    wallItem.Rotation
                )
            )
            {
                ctx.AddWallItemMovement(
                    wallItem,
                    value,
                    wallItem.Y,
                    wallItem.Z,
                    wallItem.Rotation,
                    wallItem.WallOffset
                );

                return true;
            }
        }

        return false;
    }
}
