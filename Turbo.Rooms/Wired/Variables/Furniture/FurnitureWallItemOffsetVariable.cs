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

    protected override WiredVariableValue GetValueForItem(IRoomWallItem item) => item.WallOffset;

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

        await ctx.ProcessWallItemMovementAsync(item, item.X, item.Y, item.Z, item.Rotation, value);

        return true;
    }
}
