using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurniturePositionYVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@position.y",
                WiredVariableIdBuilder.WiredVarSubBand.Position,
                30
            ),
            VariableName = "@position.y",
            VariableType = WiredVariableType.Internal,
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
        value = default;

        var snapshot = GetVarSnapshot();

        if (
            (binding.TargetType != snapshot.TargetType)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
        )
            return false;

        value = item.Y;

        return true;
    }

    public override async Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        var snapshot = GetVarSnapshot();

        if (
            (binding.TargetType != snapshot.TargetType)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
        )
            return false;

        if (item is IRoomFloorItem floorItem)
        {
            if (
                await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                    ctx.AsActionContext(),
                    floorItem.ObjectId.Value,
                    floorItem.X,
                    value,
                    floorItem.Rotation
                )
            )
            {
                ctx.AddFloorItemMovement(
                    floorItem,
                    _roomGrain.MapModule.ToIdx(floorItem.X, value),
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
                    wallItem.X,
                    value,
                    wallItem.Z,
                    wallItem.WallOffset,
                    wallItem.Rotation
                )
            )
            {
                ctx.AddWallItemMovement(
                    wallItem,
                    wallItem.X,
                    value,
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
