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

        if (GetTileIdForFloorItem(item, out var tileIds))
        {
            foreach (var id in tileIds)
            {
                _state.TileFloorStacks[id].Add(item.ObjectId.Value);

                _roomMap.ComputeTile(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureFloorItemSnapshot item,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        var newId = _roomMap.GetTileId(newX, newY);
        var roomItem = _itemsLoader.CreateFromFurnitureItemSnapshot(item);

        if (roomItem is not IRoomFloorItem floorItem)
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        floorItem.SetPosition(newX, newY, _state.TileHeights[newId]);
        floorItem.SetRotation(newRotation);

        return await AddFloorItemAsync(floorItem, ct);
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (item.X == newX && item.Y == newY && item.Rotation == newRotation)
            return false;

        var newId = _roomMap.GetTileId(newX, newY);

        if (GetTileIdForFloorItem(item, out var oldTileIds))
        {
            foreach (var id in oldTileIds)
            {
                _state.TileFloorStacks[id].Remove(item.ObjectId.Value);

                _roomMap.ComputeTile(id);
            }
        }

        item.SetPosition(newX, newY, _state.TileHeights[newId]);
        item.SetRotation(newRotation);

        if (GetTileIdForFloorItem(item, out var newTileIds))
        {
            foreach (var id in newTileIds)
            {
                _state.TileFloorStacks[id].Add(item.ObjectId.Value);

                _roomMap.ComputeTile(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (GetTileIdForFloorItem(item, out var tileIds))
        {
            foreach (var id in tileIds)
            {
                _state.TileFloorStacks[id].Remove(itemId);

                _roomMap.ComputeTile(id);
            }
        }

        await item.Logic.OnDetachAsync(ct);

        item.SetAction(null);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(ctx.PlayerId), ct);

        await FlushDirtyFloorItemNowAsync(itemId, ct);

        _state.FloorItemsById.Remove(itemId);

        return true;
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int param,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.TryGetValue(objectId.Value, out var item))
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
        int newX,
        int newY,
        Rotation newRotation
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var tItem))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (
            _roomMap.GetTileIdForSize(
                newX,
                newY,
                newRotation,
                tItem.Definition.Width,
                tItem.Definition.Length,
                out var tileIds
            )
        )
        {
            foreach (var id in tileIds)
            {
                var tileFlags = _state.TileFlags[id];
                var tileHeight = _state.TileHeights[id];
                var highestItemId = _state.TileHighestFloorItems[id];
                var isRotating = false;

                IRoomFloorItem? bItem = null;

                if (highestItemId > 0)
                {
                    bItem = _state.FloorItemsById[highestItemId];
                    isRotating = bItem == tItem && tItem.Rotation != newRotation;
                }

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + tItem.Definition.StackHeight) > _roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked) && !isRotating
                    || (
                        !_roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                        && !isRotating
                    )
                    || tileFlags.Has(RoomTileFlags.AvatarOccupied) && !tItem.Logic.CanWalk()
                )
                    return false;

                if (isRotating)
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
        FurnitureFloorItemSnapshot item,
        int newX,
        int newY,
        Rotation newRotation
    )
    {
        if (
            _roomMap.GetTileIdForSize(
                newX,
                newY,
                newRotation,
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
        _state.DirtyItemIds.Add(itemId);
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

    private async Task FlushDirtyFloorItemNowAsync(long itemId, CancellationToken ct)
    {
        try
        {
            if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
                throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

            _state.DirtyItemIds.Remove(itemId);

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

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // TODO output log
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
