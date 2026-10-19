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
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<FurniturePlacementResultType> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        try
        {
            return await ActionModule.PlaceWallItemAsync(ctx, item, x, y, z, wallOffset, rot, ct)
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
                "Failed to place wall item {ItemId} in room {RoomId} for player {PlayerId}",
                item.ItemId,
                _state.RoomId,
                ctx.PlayerId
            );

            return FurniturePlacementResultType.Refused;
        }
    }

    public async Task<FurniturePlacementResultType> PlacePostItAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        CancellationToken ct
    )
    {
        try
        {
            return await ActionModule.PlacePostItAsync(ctx, padId, location, ct)
                ? FurniturePlacementResultType.Done
                : FurniturePlacementResultType.Refused;
        }
        catch (TurboException ex) when (IsBuildRefusal(ex))
        {
            return FurniturePlacementResultType.NotAllowed;
        }
        catch (TurboException ex) when (ex.ErrorCode == TurboErrorCodeEnum.FurniTypeLimitReached)
        {
            return FurniturePlacementResultType.TypeLimit;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to place a sheet of post-it pad {ItemId} in room {RoomId} for player {PlayerId}",
                padId,
                _state.RoomId,
                ctx.PlayerId
            );

            return FurniturePlacementResultType.Refused;
        }
    }

    public async Task<bool> AddSpamWallPostItAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        string color,
        string text,
        CancellationToken ct
    )
    {
        try
        {
            return await ActionModule.AddSpamWallPostItAsync(ctx, padId, location, color, text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to pin a note from post-it pad {ItemId} in room {RoomId} for player {PlayerId}",
                padId,
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
        }
    }

    public async Task<FurniturePlacementResultType> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int newX,
        int newY,
        Altitude newZ,
        int wallOffset,
        Rotation newRot,
        CancellationToken ct
    )
    {
        try
        {
            return await ActionModule.MoveWallItemByIdAsync(
                ctx,
                itemId,
                newX,
                newY,
                newZ,
                wallOffset,
                newRot,
                ct
            )
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
                "Failed to move wall item {ItemId} in room {RoomId} for player {PlayerId}",
                itemId,
                _state.RoomId,
                ctx.PlayerId
            );

            return FurniturePlacementResultType.Refused;
        }
    }

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.ItemsById.TryGetValue(itemId, out var item)
                ? item is IRoomWallItem wall
                    ? wall.GetSnapshot()
                    : null
                : null
        );
}
