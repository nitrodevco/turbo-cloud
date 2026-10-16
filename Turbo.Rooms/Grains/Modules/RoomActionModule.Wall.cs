using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
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

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(item.OwnerId);

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
            beforeShown: () => inventory.RemoveFurnitureAsync(item.ObjectId, ct)
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
