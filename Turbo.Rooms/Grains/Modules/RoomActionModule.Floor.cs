using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
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
            !await _roomGrain.SecurityModule.CanPlaceFurniAsync(ctx)
            && !_roomGrain.FurniModule.HasBuildAreaRights(ctx.PlayerId, floorItem, x, y, rot)
        )
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

        // The spot and the room's limits are checked by the placement itself.
        if (!await _roomGrain.FurniModule.PlaceFloorItemAsync(ctx, floorItem, x, y, rot, ct))
            return false;

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(item.OwnerId);

        await inventory.RemoveFurnitureAsync(item.ObjectId, ct);

        return true;
    }

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
            !await _roomGrain.SecurityModule.CanManipulateFurniAsync(ctx)
            && !(
                _roomGrain.FurniModule.TryGetFloorItem(itemId, out var own)
                && own.OwnerId == ctx.PlayerId
                && _roomGrain.FurniModule.HasBuildAreaRights(
                    ctx.PlayerId,
                    own,
                    own.X,
                    own.Y,
                    own.Rotation
                )
                && _roomGrain.FurniModule.HasBuildAreaRights(ctx.PlayerId, own, x, y, rot)
            )
        )
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (!_roomGrain.FurniModule.CanPlaceFloorItem(itemId, x, y, rot))
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (!await _roomGrain.FurniModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, null, rot, ct))
            return false;

        return true;
    }
}
