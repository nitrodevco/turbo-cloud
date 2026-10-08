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

    // After @owner_id, before the projectile variables, as the official list has it.
    protected override ushort Order => 25;
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
            || !await FurniModule.ValidateWallItemPlacementAsync(
                ctx.AsActionContext(),
                item.ObjectId,
                item.X,
                item.Y,
                item.Z,
                value.ClampToInt(),
                item.Rotation
            )
        )
            return false;

        await ctx.ProcessWallItemMovementAsync(
            item,
            item.X,
            item.Y,
            item.Z,
            item.Rotation,
            value.ClampToInt()
        );

        return true;
    }
}
