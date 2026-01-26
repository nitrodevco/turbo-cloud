using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureRotationVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@rotation";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Position;
    protected override ushort Order => 20;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.AlwaysAvailable;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = (int)item.Rotation;

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
        )
            return false;

        switch (item)
        {
            case IRoomFloorItem floorItem:
                {
                    if (
                        await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                            ctx.AsActionContext(),
                            floorItem.ObjectId,
                            floorItem.X,
                            floorItem.Y,
                            (Rotation)value.Value
                        )
                    )
                    {
                        ctx.AddFloorItemMovement(
                            floorItem,
                            _roomGrain.MapModule.ToIdx(floorItem.X, floorItem.Y),
                            floorItem.Z,
                            (Rotation)value.Value
                        );

                        return true;
                    }
                }
                break;
            case IRoomWallItem wallItem:
                {
                    var rot =
                        (Rotation)value.Value == Rotation.South ? Rotation.South : Rotation.North;

                    if (
                        await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                            ctx.AsActionContext(),
                            wallItem.ObjectId,
                            wallItem.X,
                            wallItem.Y,
                            wallItem.Z,
                            wallItem.WallOffset,
                            rot
                        )
                    )
                    {
                        ctx.AddWallItemMovement(
                            wallItem,
                            wallItem.X,
                            wallItem.Y,
                            wallItem.Z,
                            rot,
                            wallItem.WallOffset
                        );

                        return true;
                    }
                }
                break;
        }

        return false;
    }
}
