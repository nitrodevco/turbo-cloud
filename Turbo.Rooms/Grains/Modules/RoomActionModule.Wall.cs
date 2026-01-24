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
        if (!await _roomGrain.SecurityModule.CanPlaceFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

        var item = _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);

        if (item is not IRoomWallItem wallItem)
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (
            !await _roomGrain.FurniModule.ValidateNewWallItemPlacementAsync(
                ctx,
                wallItem,
                x,
                y,
                z,
                wallOffset,
                rot
            )
        )
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (
            !await _roomGrain.FurniModule.PlaceWallItemAsync(
                ctx,
                wallItem,
                x,
                y,
                z,
                wallOffset,
                rot,
                ct
            )
        )
            return false;

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(item.OwnerId);

        await inventory.RemoveFurnitureAsync(item.ObjectId, ct);

        return true;
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
        if (!await _roomGrain.SecurityModule.CanManipulateFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (
            !await _roomGrain.FurniModule.ValidateWallItemPlacementAsync(
                ctx,
                itemId,
                x,
                y,
                z,
                wallOffset,
                rot
            )
        )
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (
            !await _roomGrain.FurniModule.MoveWallItemByIdAsync(
                ctx,
                itemId,
                x,
                y,
                z,
                wallOffset,
                rot,
                ct
            )
        )
            return false;

        return true;
    }
}
