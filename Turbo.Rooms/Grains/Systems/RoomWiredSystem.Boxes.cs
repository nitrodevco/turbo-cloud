using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// What the room asks about single wired boxes: may another be placed, what does its editor
/// show, and saving it. Kept here so the placement and furniture code never has to recognise a
/// wired box.
/// </summary>
public sealed partial class RoomWiredSystem : IRoomPlacementLimit
{
    public void EnsureCanPlace(IRoomItem item)
    {
        if (item.Logic is not IWiredBox)
            return;

        var (floor, wall) = CountWiredItems();

        if (item is IRoomWallItem)
        {
            if (wall >= _roomGrain._wiredConfig.MaxWallItems)
                throw new TurboException(TurboErrorCodeEnum.WiredWallItemLimitReached);
        }
        else if (floor >= _roomGrain._wiredConfig.MaxFloorItems)
        {
            throw new TurboException(TurboErrorCodeEnum.WiredFloorItemLimitReached);
        }
    }

    /// <summary>A box's editor data, or null when the item is not a wired box.</summary>
    public WiredDataSnapshot? GetBoxSnapshot(RoomObjectId itemId) =>
        _roomGrain.FurniModule.TryGetItem(itemId, out var item)
        && item.Logic is FurnitureWiredLogic wiredLogic
            ? wiredLogic.GetSnapshot()
            : null;

    /// <summary>
    /// Saves a box from its editor. The room's wired permission is not enough for a box that
    /// asks for more (<see cref="FurnitureWiredLogic.MinimumControllerLevelToSave"/>).
    /// </summary>
    public async Task<bool> ApplyUpdateAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain.FurniModule.TryGetItem(itemId, out var item)
            || item.Logic is not FurnitureWiredLogic wiredLogic
        )
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);
        var (canModify, _) = GetPermissions(controllerLevel);

        if (!canModify || controllerLevel < wiredLogic.MinimumControllerLevelToSave)
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToModifyWired);

        return await wiredLogic.ApplyWiredUpdateAsync(ctx, update, ct);
    }
}
