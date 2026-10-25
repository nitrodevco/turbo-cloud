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
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule
{
    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot snapshot,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!await SecurityModule.CanPlaceFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

        var item = _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);

        if (item is not IRoomWallItem wallItem)
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        var inventory = GrainFactory.GetInventoryGrain(item.OwnerId);

        // The spot and the room's limits are checked by the placement itself. Habbo takes a
        // wall item out of the inventory before it shows it on the wall (official client
        // captures 2026-10-16 09:03-09:04, 2 of 2: FurniListRemove, then ItemAdd), unlike a
        // floor item, which is shown first.
        return await FurniModule.PlaceWallItemAsync(
            ctx,
            wallItem,
            x,
            y,
            z,
            wallOffset,
            rot,
            ct,
            beforeShown: async () =>
            {
                await WritePlacementAsync(ctx, item, ct);
                await inventory.RemoveFurnitureAsync(item.ObjectId, ct);
            }
        );
    }

    /// <summary>
    /// A player destroying a wall item outright (<c>RemoveItem</c>), as the post-it's bin and the
    /// photo viewer's remove button do - the only two senders, and only for a wall item
    /// (<c>RoomEngine.deleteRoomObject</c> acts on category 20 alone). Flash offers both from
    /// controller level 4 up: the photo's <c>hasRightsToRemove</c> (<c>roomControllerLevel &gt;= 4</c>,
    /// <c>ExternalImageWidgetHandler.as:99-102</c>) and the post-it's <c>isRoomOwner ||
    /// isAnyRoomController</c> (<c>FurnitureStickieWidgetHandler.as:81</c>), the room's owner and
    /// <c>room.control.any</c> here. Anyone else is refused: rights, or owning the item, let a
    /// player destroy any furni in the room.
    /// </summary>
    public async Task<bool> RemoveWallItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!State.ItemsById.TryGetValue(itemId, out var item) || item is not IRoomWallItem)
            return false;

        if (await SecurityModule.GetControllerLevelAsync(ctx) < RoomControllerType.Owner)
            return false;

        return await DeleteItemByIdAsync(
            ctx,
            itemId,
            MovementCause.New(MovementCauses.ROOM_DELETE_ITEM, ctx.PlayerId),
            ct
        );
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!await SecurityModule.CanManipulateFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (
            !await FurniModule.ValidateWallItemPlacementAsync(ctx, itemId, x, y, z, wallOffset, rot)
        )
            // A refusal, as for a floor item: the caller puts it back and tells the mover.
            return false;

        if (!await FurniModule.MoveWallItemByIdAsync(ctx, itemId, x, y, z, wallOffset, rot, ct))
            return false;

        return true;
    }
}
