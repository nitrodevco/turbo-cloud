using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureWallItemOffsetVariable(RoomGrain roomGrain)
    : FurnitureWallVariable(roomGrain)
{
    protected override string VariableName => "@wallitem_offset";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;
    protected override ushort Order => 10;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;

    protected override bool TryGetValueForItem(IRoomWallItem item, out WiredVariableValue value)
    {
        value = item.WallOffset;

        return true;
    }

    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        var snapshot = GetVarSnapshot();

        if (
            !snapshot.Flags.Has(WiredVariableFlags.CanWriteValue)
            || !CanBind(key)
            || !TryGetItemForKey(key, out var item)
            || item is null
            || !await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                ctx.AsActionContext(),
                item.ObjectId,
                item.X,
                item.Y,
                item.Z,
                value,
                item.Rotation
            )
        )
            return false;

        ctx.AddWallItemMovement(item, item.X, item.Y, item.Z, item.Rotation, value);

        return true;
    }
}
