using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule
{
    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (!_state.FloorItemsById.TryAdd(item.ObjectId.Value, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        item.SetAction(objectId => ProcessDirtyFloorItem(objectId.Value, ct));

        await AttatchFloorLogicIfNeededAsync(item, ct);

        if (!_roomMap.AddFloorItem(item, true, out var updatedTileIds))
            return false;

        await _roomMap.InvokeAvatarsOnTilesAsync(updatedTileIds, ct);

        return true;
    }

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot snapshot,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        var tileIdx = _roomMap.ToIdx(x, y);

        if (!_roomMap.InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        var roomItem = _itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);

        if (roomItem is not IRoomFloorItem item)
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!_state.FloorItemsById.TryAdd(item.ObjectId.Value, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        item.SetAction(objectId => ProcessDirtyFloorItem(objectId.Value, ct));

        await AttatchFloorLogicIfNeededAsync(item, ct);

        if (!_roomMap.PlaceFloorItem(item, tileIdx, rot, true, out var updatedTileIds))
            return false;

        await FlushDirtyFloorItemAsync(item.ObjectId.Value, ct);

        await _roomMap.InvokeAvatarsOnTilesAsync(updatedTileIds, ct);

        return true;
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var pTileIdx = _roomMap.ToIdx(item.X, item.Y);
        var nTileIdx = _roomMap.ToIdx(x, y);

        if (!_roomMap.MoveFloorItem(item, nTileIdx, rot, true, out var updatedTileIds))
            return false;

        await item.Logic.OnMoveAsync(ctx, ct);

        await _roomMap.InvokeAvatarsOnTilesAsync(updatedTileIds, ct);

        return true;
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int pickerId = -1
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!_roomMap.RemoveFloorItem(item, pickerId, true, out var updatedTileIds))
            return false;

        await _roomMap.InvokeAvatarsOnTilesAsync(updatedTileIds, ct);

        await item.Logic.OnDetachAsync(ct);

        item.SetAction(null);

        await FlushDirtyFloorItemAsync(itemId, ct);

        _state.FloorItemsById.Remove(itemId);

        return true;
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
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
        int itemId,
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
        int itemId,
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

                IRoomFloorItem? bItem = null;

                if (highestItemId > 0)
                    bItem = _state.FloorItemsById[highestItemId];

                if (bItem == tItem)
                {
                    tileHeight -= tItem.Definition.StackHeight;

                    if (tItem.Rotation != rot)
                        isRotating = true;
                }

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + tItem.Definition.StackHeight) > _roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked) && bItem != tItem
                    || (
                        !_roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                        && !isRotating
                    )
                    || tileFlags.Has(RoomTileFlags.AvatarOccupied) && !tItem.Logic.CanWalk()
                )
                    return false;

                if (bItem == tItem)
                    continue;

                if (bItem is not null && bItem != tItem)
                {
                    // if is a stack helper, allow placement
                    // if is a roller, disallow placement
                }
            }
        }

        return true;
    }

    public bool ValidateNewFloorItemPlacement(
        ActionContext ctx,
        FurnitureItemSnapshot item,
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

                if (highestItemId > 0)
                    bItem = _state.FloorItemsById[highestItemId];

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + item.Definition.StackHeight) > _roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked)
                    || (
                        !_roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                    )
                    || tileFlags.Has(RoomTileFlags.AvatarOccupied) && !item.Definition.CanWalk
                )
                    return false;

                if (bItem is not null)
                {
                    // if is a stack helper, allow placement
                    // if is a roller, disallow placement
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
            _state.FloorItemsById.TryGetValue(objectId.Value, out var item)
                ? item.GetSnapshot()
                : null
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

    private async Task FlushDirtyFloorItemAsync(long itemId, CancellationToken ct)
    {
        try
        {
            if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
                throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

            _state.DirtyFloorItemIds.Remove(itemId);

            var snapshot = item.GetSnapshot();

            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.Furnitures.SingleAsync(
                x => x.Id == snapshot.ObjectId.Value,
                ct
            );

            entity.X = snapshot.X;
            entity.Y = snapshot.Y;
            entity.Z = snapshot.Z;
            entity.Rotation = snapshot.Rotation;
            entity.StuffData = snapshot.StuffDataJson;
            entity.RoomEntityId = (int)_state.RoomId;

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
            var snapshots = dirtyIds
                .Select(id =>
                    _state.FloorItemsById.TryGetValue(id, out var item) ? item.GetSnapshot() : null
                )
                .ToArray();
            var ids = snapshots.Where(x => x is not null).Select(x => x!.ObjectId.Value).ToArray();
            var entities = await dbCtx
                .Furnitures.Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var snapshot in snapshots)
            {
                if (
                    snapshot is null
                    || !entities.TryGetValue(snapshot.ObjectId.Value, out var entity)
                )
                    continue;

                entity.X = snapshot.X;
                entity.Y = snapshot.Y;
                entity.Z = snapshot.Z;
                entity.Rotation = snapshot.Rotation;
                entity.StuffData = snapshot.StuffDataJson;
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

    internal Task ComputeRollerItemsAsync(CancellationToken ct)
    {
        _state.RollerInfos.Clear();

        foreach (var item in _state.FloorItemsById.Values)
        {
            if (item.Logic is not FurnitureRollerLogic rollerLogic)
                continue;

            _state.RollerInfos.Add(
                new RollerInfoSnapshot
                {
                    ObjectId = item.ObjectId,
                    X = item.X,
                    Y = item.Y,
                    TargetX = item.X,
                    TargetY = item.Y,
                }
            );
        }

        return Task.CompletedTask;
    }
}
