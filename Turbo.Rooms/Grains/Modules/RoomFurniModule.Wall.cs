using System;
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
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule
{
    public async Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (!_state.WallItemsById.TryAdd(item.ObjectId.Value, item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!_state.OwnerNamesById.TryGetValue(item.OwnerId, out string? value))
        {
            var ownerName = await _grainFactory
                .GetGrain<IPlayerDirectoryGrain>(PlayerDirectoryGrain.SINGLETON_KEY)
                .GetPlayerNameAsync(item.OwnerId, ct);

            value = ownerName;
            _state.OwnerNamesById[item.OwnerId] = value;
        }

        item.SetOwnerName(value ?? string.Empty);
        item.SetAction(objectId => ProcessDirtyWallItem(objectId.Value, ct));

        await AttatchWallLogicIfNeededAsync(item, ct);

        if (!_roomMap.AddWallItem(item))
            return false;

        return true;
    }

    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryAdd(item.ObjectId.Value, item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!_state.OwnerNamesById.TryGetValue(item.OwnerId, out string? value))
        {
            var ownerName = await _grainFactory
                .GetGrain<IPlayerDirectoryGrain>(PlayerDirectoryGrain.SINGLETON_KEY)
                .GetPlayerNameAsync(item.OwnerId, ct);

            value = ownerName;
            _state.OwnerNamesById[item.OwnerId] = value;
        }

        item.SetOwnerName(value ?? string.Empty);
        item.SetAction(objectId => ProcessDirtyWallItem(objectId.Value, ct));

        await AttatchWallLogicIfNeededAsync(item, ct);

        if (!_roomMap.PlaceWallItem(item, x, y, z, rot, wallOffset))
            return false;

        await FlushDirtyWallItemAsync(item.ObjectId.Value, ct);

        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!_roomMap.MoveWallItemItem(item, x, y, z, rot, wallOffset))
            return false;

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<RoomWallItemSnapshot?> RemoveWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int pickerId = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (pickerId == -1)
            pickerId = (int)item.OwnerId;

        item.SetOwnerId(pickerId);

        if (!_roomMap.RemoveWallItem(item, pickerId))
            return null;

        await item.Logic.OnDetachAsync(ct);

        item.SetOwnerId(pickerId);
        item.SetAction(null);

        await FlushDirtyWallItemAsync(itemId, ct, true);

        _state.WallItemsById.Remove(itemId);

        return item.GetSnapshot();
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ValidateWallItemPlacementAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    public Task<bool> ValidateNewWallItemPlacementAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    public Task<ImmutableArray<RoomWallItemSnapshot>> GetAllWallItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.WallItemsById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.WallItemsById.TryGetValue(objectId.Value, out var item)
                ? item.GetSnapshot()
                : null
        );

    private void ProcessDirtyWallItem(int itemId, CancellationToken ct)
    {
        _state.DirtyWallItemIds.Add(itemId);
    }

    private async Task AttatchWallLogicIfNeededAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (item.Logic is not null)
            return;

        var logicType = item.Definition.LogicName;
        var ctx = new RoomWallItemContext(_roomGrain, this, item);
        var logic = _logicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IFurnitureWallLogic wallLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        item.SetLogic(wallLogic);

        await logic.OnAttachAsync(ct);
    }

    private async Task FlushDirtyWallItemAsync(
        long itemId,
        CancellationToken ct,
        bool remove = false
    )
    {
        try
        {
            if (!_state.WallItemsById.TryGetValue(itemId, out var item))
                throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

            _state.DirtyWallItemIds.Remove(itemId);

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
            entity.WallOffset = snapshot.WallOffset;
            entity.StuffData = snapshot.StuffDataJson;
            entity.PlayerEntityId = (int)snapshot.OwnerId;
            entity.RoomEntityId = remove ? null : (int)_state.RoomId;

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // TODO output log
        }
    }

    internal async Task FlushDirtyWallItemsAsync(CancellationToken ct)
    {
        if (_state.DirtyWallItemIds.Count == 0)
            return;

        var dirtyIds = _state.DirtyWallItemIds.ToArray();

        _state.DirtyWallItemIds.Clear();

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var snapshots = dirtyIds
                .Select(id =>
                    _state.WallItemsById.TryGetValue(id, out var item) ? item.GetSnapshot() : null
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
                entity.WallOffset = snapshot.WallOffset;
                entity.Rotation = snapshot.Rotation;
                entity.StuffData = snapshot.StuffDataJson;
                entity.PlayerEntityId = (int)snapshot.OwnerId;
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
