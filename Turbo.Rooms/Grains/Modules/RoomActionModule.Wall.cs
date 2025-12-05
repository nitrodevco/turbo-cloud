using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        return _furniModule.AddWallItemAsync(item, ct);
    }

    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureWallItemSnapshot item,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot,
        CancellationToken ct
    )
    {
        if (!await _securityModule.CanPlaceFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (
            !await _furniModule.ValidateNewWallItemPlacementAsync(
                ctx,
                item,
                newX,
                newY,
                newZ,
                wallOffset,
                newRot
            )
        )
            throw new TurboException(TurboErrorCodeEnum.InvalidFloorItemPlacement);

        if (
            !await _furniModule.PlaceWallItemAsync(
                ctx,
                item,
                newX,
                newY,
                newZ,
                wallOffset,
                newRot,
                ct
            )
        )
            return false;

        // TODO add player name to owner names cache

        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot,
        CancellationToken ct
    )
    {
        if (!await _securityModule.CanManipulateFurniAsync(ctx))
            return false;

        if (
            !await _furniModule.ValidateWallItemPlacementAsync(
                ctx,
                itemId,
                newX,
                newY,
                newZ,
                wallOffset,
                newRot
            )
        )
            throw new TurboException(TurboErrorCodeEnum.InvalidWallItemPlacement);

        if (
            !await _furniModule.MoveWallItemByIdAsync(
                ctx,
                itemId,
                newX,
                newY,
                newZ,
                wallOffset,
                newRot,
                ct
            )
        )
            return false;

        return true;
    }

    public Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct
    ) => _furniModule.RemoveWallItemByIdAsync(ctx, itemId, ct);

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        var controllerLevel = await _securityModule.GetControllerLevelAsync(ctx);
        var usagePolicy = item.Logic.GetUsagePolicy();

        if (usagePolicy == FurnitureUsageType.Nobody)
            return false;

        if (usagePolicy == FurnitureUsageType.Controller)
        {
            if (controllerLevel < RoomControllerType.Rights)
                return false;
        }

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    ) => _furniModule.ClickWallItemByIdAsync(ctx, itemId, ct, param);
}
