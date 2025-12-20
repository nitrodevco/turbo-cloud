using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Logging;
using Turbo.Players.Grains;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule
{
    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (!await AttatchFloorItemAsync(item, ct) || !_roomMap.AddFloorItem(item, true))
            return false;

        return true;
    }

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        var tileIdx = _roomMap.ToIdx(x, y);

        if (!_roomMap.InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        if (
            !await AttatchFloorItemAsync(item, ct)
            || !_roomMap.PlaceFloorItem(item, tileIdx, rot, true)
        )
            return false;

        await FlushDirtyFloorItemAsync(item.ObjectId, ct);

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
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var nTileIdx = _roomMap.ToIdx(x, y);

        if (!_roomMap.MoveFloorItem(item, nTileIdx, rot, true))
            return false;

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<RoomFloorItemSnapshot?> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int pickerId = -1
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (pickerId == -1)
            pickerId = item.OwnerId;

        if (!_roomMap.RemoveFloorItem(item, pickerId, true))
            return null;

        await item.Logic.OnDetachAsync(ct);

        item.SetOwnerId(pickerId);
        item.SetAction(null);

        await FlushDirtyFloorItemAsync(itemId, ct, true);

        _state.FloorItemsById.Remove(itemId);

        return item.GetSnapshot();
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public bool ValidateFloorItemPlacement(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var tItem))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (
            _roomMap.GetTileIdForSize(
                x,
                y,
                rot,
                tItem.Definition.Width,
                tItem.Definition.Length,
                out var tileIds
            )
        )
        {
            foreach (var idx in tileIds)
            {
                var tileFlags = _state.TileFlags[idx];
                var tileHeight = _state.TileHeights[idx];
                var highestItemId = _state.TileHighestFloorItems[idx];
                var isRotating = false;

                if (_state.FloorItemsById.TryGetValue(highestItemId, out var bItem))
                {
                    if (bItem == tItem)
                    {
                        tileHeight -= tItem.GetStackHeight();

                        if (tItem.Rotation != rot)
                            isRotating = true;
                    }
                }

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + tItem.GetStackHeight()) > _roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked) && bItem != tItem
                    || !_roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                        && !isRotating
                    || tileFlags.Has(RoomTileFlags.AvatarOccupied) && !tItem.Logic.CanWalk()
                )
                    return false;

                if (bItem == tItem)
                    continue;

                if (bItem is not null && bItem != tItem)
                {
                    if (
                        bItem.Logic is FurnitureRollerLogic
                        && (
                            tItem.Definition.Width > 1
                            || tItem.Definition.Length > 1
                            || tItem.Logic is FurnitureRollerLogic
                        )
                    )
                        return false;

                    // if is a stack helper, allow placement
                }
            }
        }

        return true;
    }

    public bool ValidateNewFloorItemPlacement(
        ActionContext ctx,
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rot
    )
    {
        if (
            _roomMap.GetTileIdForSize(
                x,
                y,
                rot,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
        {
            foreach (var id in tileIds)
            {
                var tileFlags = _state.TileFlags[id];
                var tileHeight = _state.TileHeights[id];
                var highestItemId = _state.TileHighestFloorItems[id];
                IRoomFloorItem? bItem = null;

                if (_state.FloorItemsById.TryGetValue(highestItemId, out var floorItem))
                    bItem = floorItem;

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + item.GetStackHeight()) > _roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked)
                    || !_roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                    || tileFlags.Has(RoomTileFlags.AvatarOccupied) && !item.Logic.CanWalk()
                )
                    return false;

                if (bItem is not null)
                {
                    if (
                        bItem.Logic is FurnitureRollerLogic
                        && (
                            item.Definition.Width > 1
                            || item.Definition.Length > 1
                            || item.Logic is FurnitureRollerLogic
                        )
                    )
                        return false;
                    // if is a stack helper, allow placement
                }
            }
        }

        return true;
    }

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.TryGetValue(objectId, out var item) ? item.GetSnapshot() : null
        );

    public bool GetTileIdForFloorItem(IRoomFloorItem item, out List<int> tileIds) =>
        _roomMap.GetTileIdForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Length,
            out tileIds
        );

    private async Task<bool> AttatchFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (!_state.FloorItemsById.TryAdd(item.ObjectId, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!_state.OwnerNamesById.TryGetValue(item.OwnerId, out string? value))
        {
            var ownerName = await _grainFactory
                .GetGrain<IPlayerDirectoryGrain>(PlayerDirectoryGrain.SINGLETON_KEY)
                .GetPlayerNameAsync(item.OwnerId, ct);

            value = ownerName;
            _state.OwnerNamesById[item.OwnerId] = value;
        }

        item.SetOwnerName(value ?? string.Empty);
        item.SetAction(objectId => ProcessDirtyFloorItem(objectId, ct));

        await AttatchFloorLogicIfNeededAsync(item, ct);

        return true;
    }

    private void ProcessDirtyFloorItem(int itemId, CancellationToken ct)
    {
        _state.DirtyFloorItemIds.Add(itemId);
    }

    private async Task AttatchFloorLogicIfNeededAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (item.Logic is not null)
            return;

        var logicType = item.Definition.LogicName;
        var ctx = new RoomFloorItemContext(_roomGrain, this, item);
        var logic = _logicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IFurnitureFloorLogic floorLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        item.SetLogic(floorLogic);

        await logic.OnAttachAsync(ct);
    }

    private async Task FlushDirtyFloorItemAsync(
        RoomObjectId itemId,
        CancellationToken ct,
        bool remove = false
    )
    {
        try
        {
            if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
                throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

            _state.DirtyFloorItemIds.Remove(itemId);

            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.Furnitures.SingleAsync(x => x.Id == (int)item.ObjectId, ct);

            entity.X = item.X;
            entity.Y = item.Y;
            entity.Z = item.Z;
            entity.Rotation = item.Rotation;
            entity.StuffData = item.Logic?.StuffData.ToJson();
            entity.PlayerEntityId = (int)item.OwnerId;
            entity.RoomEntityId = remove ? null : (int)_state.RoomId;

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // TODO output log
        }
    }

    internal async Task FlushDirtyFloorItemsAsync(CancellationToken ct)
    {
        if (_state.DirtyFloorItemIds.Count == 0)
            return;

        var dirtyIds = _state.DirtyFloorItemIds.ToArray();

        _state.DirtyFloorItemIds.Clear();

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var entities = await dbCtx
                .Furnitures.Where(x => dirtyIds.Select(x => (int)x).Contains(x.Id))
                .ToListAsync(ct);

            foreach (var entity in entities)
            {
                if (!_state.FloorItemsById.TryGetValue(entity.Id, out var item))
                    continue;

                entity.X = item.X;
                entity.Y = item.Y;
                entity.Z = item.Z;
                entity.Rotation = item.Rotation;
                entity.StuffData = item.Logic?.StuffData.ToJson();
                entity.PlayerEntityId = (int)item.OwnerId;
                entity.RoomEntityId = (int)_state.RoomId;
            }

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }
}
