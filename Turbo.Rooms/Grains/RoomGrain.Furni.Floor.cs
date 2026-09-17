using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> PlaceFloorItemAsync(
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
            if (!await ActionModule.PlaceFloorItemAsync(ctx, item, x, y, rot, ct))
                return false;

            return true;
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

            return false;
        }
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
        try
        {
            if (!await ActionModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct))
                return false;

            return true;
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

            return false;
        }
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
            if (!await ActionModule.ApplyWiredUpdateAsync(ctx, itemId, update, ct))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to apply a wired update to item {ItemId} in room {RoomId} for player {PlayerId}",
                itemId,
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
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

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => FurniModule.GetAllFloorItemSnapshotsAsync(ct);

    public async Task<WiredDataSnapshot?> GetWiredDataSnapshotByFloorItemIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        return
            _state.ItemsById.TryGetValue(itemId, out var item)
            && item.Logic is FurnitureWiredLogic wiredLogic
            ? wiredLogic.GetSnapshot()
            : null;
    }

    public async Task<WiredVariablesSnapshot?> GetWiredVariablesSnapshotAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        return await WiredSystem.GetWiredVariablesSnapshotAsync(ct);
    }

    public async Task<List<(
        WiredVariableId id,
        WiredVariableValue value
    )>?> GetAllVariablesForBindingAsync(
        ActionContext ctx,
        WiredVariableBinding binding,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        return await WiredSystem.GetAllVariablesForBindingAsync(binding, ct);
    }
}
