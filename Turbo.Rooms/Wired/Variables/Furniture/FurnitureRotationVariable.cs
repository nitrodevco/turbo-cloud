using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureRotationVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@rotation",
                WiredVariableIdBuilder.WiredVarSubBand.Position,
                20
            ),
            VariableName = "@rotation",
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

        value = (int)item.Rotation;

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
                    (Rotation)value
                )
            )
            {
                ctx.AddFloorItemMovement(
                    floorItem,
                    _roomGrain.MapModule.ToIdx(floorItem.X, floorItem.Y),
                    floorItem.Z,
                    (Rotation)value
                );

                return true;
            }
        }
        else if (item is IRoomWallItem wallItem)
        {
            var rot = (Rotation)value == Rotation.South ? Rotation.South : Rotation.North;

            if (
                await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                    ctx.AsActionContext(),
                    wallItem.ObjectId.Value,
                    value,
                    wallItem.Y,
                    wallItem.Z,
                    wallItem.WallOffset,
                    rot
                )
            )
            {
                ctx.AddWallItemMovement(
                    wallItem,
                    value,
                    wallItem.Y,
                    wallItem.Z,
                    rot,
                    wallItem.WallOffset
                );

                return true;
            }
        }

        return false;
    }
}
