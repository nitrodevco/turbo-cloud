using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureWallItemOffsetVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredInternalVariable
{
    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = WiredVariableIdBuilder.CreateInternalOrdered(
                WiredVariableTargetType.Furni,
                "@wallitem_offset",
                WiredVariableIdBuilder.WiredVarSubBand.Other,
                10
            ),
            VariableName = "@wallitem_offset",
            AvailabilityType = WiredAvailabilityType.Internal,
            TargetType = WiredVariableTargetType.Furni,
            Flags = WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue,
            TextConnectors = [],
        };

    public override bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = 0;

        if (
            !CanBind(binding)
            || !_roomGrain._state.ItemsById.TryGetValue(binding.TargetId, out var item)
            || item is not IRoomWallItem wallItem
        )
            return false;

        value = wallItem.WallOffset;

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
            || item is not IRoomWallItem wallItem
            || !await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                ctx.AsActionContext(),
                wallItem.ObjectId.Value,
                wallItem.X,
                wallItem.Y,
                wallItem.Z,
                value,
                wallItem.Rotation
            )
        )
            return false;

        ctx.AddWallItemMovement(
            wallItem,
            wallItem.X,
            wallItem.Y,
            wallItem.Z,
            wallItem.Rotation,
            value
        );

        return true;
    }
}
