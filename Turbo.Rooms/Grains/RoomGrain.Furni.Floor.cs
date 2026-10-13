using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    /// <summary>
    /// The room refused the player the right to build here: an answer for the player, not a
    /// fault, so it is neither logged nor told as a spot that does not take the item.
    /// </summary>
    private static bool IsBuildRefusal(TurboException ex) =>
        ex.ErrorCode
            is TurboErrorCodeEnum.NoPermissionToPlaceFurni
                or TurboErrorCodeEnum.NoPermissionToManipulateFurni;

    public async Task<FurniturePlacementResultType> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        try
        {
            return await ActionModule.PlaceFloorItemAsync(ctx, item, x, y, rot, ct)
                ? FurniturePlacementResultType.Done
                : FurniturePlacementResultType.Refused;
        }
        catch (TurboException ex) when (IsBuildRefusal(ex))
        {
            return FurniturePlacementResultType.NotAllowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to place item {ItemId} in room {RoomId} for player {PlayerId}",
                item.ItemId,
                _state.RoomId,
                ctx.PlayerId
            );

            return FurniturePlacementResultType.Refused;
        }
    }

    public async Task<FurniturePlacementResultType> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        try
        {
            return await ActionModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct)
                ? FurniturePlacementResultType.Done
                : FurniturePlacementResultType.Refused;
        }
        catch (TurboException ex) when (IsBuildRefusal(ex))
        {
            return FurniturePlacementResultType.NotAllowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move item {ItemId} in room {RoomId} for player {PlayerId}",
                itemId,
                _state.RoomId,
                ctx.PlayerId
            );

            return FurniturePlacementResultType.Refused;
        }
    }

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.ItemsById.TryGetValue(itemId, out var item) && item is IRoomFloorItem floorItem
                ? floorItem.GetSnapshot()
                : null
        );
}
