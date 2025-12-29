using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        return await _actionModule.AddFloorItemAsync(item, ct);
    }

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        return await _actionModule.PlaceFloorItemAsync(ctx, item, x, y, rot, ct);
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
        return await _actionModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct);
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        return await _actionModule.RemoveFloorItemByIdAsync(ctx, itemId, ct);
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        return await _actionModule.UseFloorItemByIdAsync(ctx, itemId, ct, param);
    }

    public async Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        return await _actionModule.ClickFloorItemByIdAsync(ctx, itemId, ct, param);
    }

    public async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        try
        {
            return await _actionModule.ApplyWiredUpdateAsync(ctx, itemId, update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply wired update for item {ItemId} in room {RoomId}", itemId, _roomId);
            return false;
        }
    }

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.FloorItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllFloorItemSnapshotsAsync(ct);

    public Task<WiredDataSnapshot?> GetWiredDataSnapshotByFloorItemIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.FloorItemsById.TryGetValue(itemId, out var item)
                ? item.Logic is FurnitureWiredLogic wiredLogic
                    ? wiredLogic.GetSnapshot()
                    : null
                : null
        );
}
