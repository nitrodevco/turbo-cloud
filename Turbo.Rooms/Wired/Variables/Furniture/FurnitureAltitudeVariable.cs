using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
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
        value = 0;

        if (
            !CanBind(binding)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
        )
            return false;

        value = item.Z.ToInt();
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
                    floorItem.X,
                    floorItem.Y,
                    floorItem.Rotation
                )
            )
            {
                ctx.AddFloorItemMovement(
                    floorItem,
                    _roomGrain.MapModule.ToIdx(floorItem.X, floorItem.Y),
                    Altitude.FromInt(value),
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
                    wallItem.Y,
                    Altitude.FromInt(value),
                    wallItem.WallOffset,
                    wallItem.Rotation
                )
            )
            {
                ctx.AddWallItemMovement(
                    wallItem,
                    wallItem.X,
                    wallItem.Y,
                    Altitude.FromInt(value),
                    wallItem.Rotation,
                    wallItem.WallOffset
                );

                return true;
            }
        }

        return false;
    }
}
