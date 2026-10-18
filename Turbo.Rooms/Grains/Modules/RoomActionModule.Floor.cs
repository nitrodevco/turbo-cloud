using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule
{
    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot snapshot,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        var item = _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);

        if (item is not IRoomFloorItem floorItem)
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        // Room rights, or a furni that lets this player build on exactly these tiles.
        if (
            !await SecurityModule.CanPlaceFurniAsync(ctx)
            && !FurniModule.HasBuildAreaRights(ctx.PlayerId, floorItem, x, y, rot)
        )
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

        // The spot and the room's limits are checked by the placement itself. The owner hears
        // of it first, so their inventory's removal follows it.
        if (
            !await FurniModule.PlaceFloorItemAsync(
                ctx,
                floorItem,
                x,
                y,
                rot,
                ct,
                toldFirst: item.OwnerId
            )
        )
            return false;

        await WritePlacementAsync(ctx, item, ct);

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(item.OwnerId);

        await inventory.RemoveFurnitureAsync(item.ObjectId, ct);

        return true;
    }

    /// <summary>
    /// Writes now that the item left its owner's inventory for this room, before the inventory
    /// lets go of it: a row still in the inventory after the room took it could be traded away.
    /// </summary>
    private Task WritePlacementAsync(ActionContext ctx, IRoomItem item, CancellationToken ct) =>
        _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .WritePlacementAsync(
                _roomGrain.RoomId,
                item.GetSnapshot(),
                MovementCause.New(MovementCauses.ROOM_PLACE, ctx.PlayerId),
                ct
            );

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        // Without room rights a player still moves their own furni inside an area they may
        // build on: it has to stand there now and end up there.
        if (
            !await SecurityModule.CanManipulateFurniAsync(ctx)
            && !(
                FurniModule.TryGetFloorItem(itemId, out var own)
                && own.OwnerId == ctx.PlayerId
                && FurniModule.HasBuildAreaRights(ctx.PlayerId, own, own.X, own.Y, own.Rotation)
                && FurniModule.HasBuildAreaRights(ctx.PlayerId, own, x, y, rot)
            )
        )
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        // A spot that does not take the item is a refusal, not a fault: the caller puts the furni
        // back and tells the mover, as it does for any other refused move.
        if (!FurniModule.CanPlaceFloorItem(itemId, x, y, rot))
            return false;

        if (!await FurniModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, null, rot, ct))
            return false;

        return true;
    }
}
