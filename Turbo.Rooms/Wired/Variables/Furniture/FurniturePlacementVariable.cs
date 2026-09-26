using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// A variable that is one component of where a furni stands (x, y, altitude, rotation).
/// Writing it moves the furni: the new placement is validated like a player's move and goes
/// out through the wired movement packet. A subclass only says which component it is.
/// </summary>
public abstract class FurniturePlacementVariable(RoomGrain roomGrain)
    : FurnitureValueVariable<IRoomItem>(roomGrain)
{
    protected readonly record struct Placement(int X, int Y, Altitude Z, Rotation Rotation);

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Position;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.AlwaysAvailable;

    /// <summary>The item's placement with this variable's component replaced by the value.</summary>
    protected abstract Placement Apply(IRoomItem item, Placement current, int value);

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
        )
            return false;

        var target = Apply(item, new Placement(item.X, item.Y, item.Z, item.Rotation), value);

        switch (item)
        {
            case IRoomFloorItem floorItem:
                return await ctx.TryMoveFloorItemAsync(
                    floorItem,
                    target.X,
                    target.Y,
                    target.Z,
                    target.Rotation
                );
            case IRoomWallItem wallItem:
                if (
                    !await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                        ctx.AsActionContext(),
                        wallItem.ObjectId,
                        target.X,
                        target.Y,
                        target.Z,
                        wallItem.WallOffset,
                        target.Rotation
                    )
                )
                    return false;

                await ctx.ProcessWallItemMovementAsync(
                    wallItem,
                    target.X,
                    target.Y,
                    target.Z,
                    target.Rotation,
                    wallItem.WallOffset
                );

                return true;
            default:
                return false;
        }
    }
}
