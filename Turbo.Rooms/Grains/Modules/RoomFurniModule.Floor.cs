using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule
{
    public bool GetTileIdForFloorItem(IRoomFloorItem item, out List<int> tileIds) =>
        _roomMap.GetTileIdForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Height,
            out tileIds
        );

    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (!_state.FloorItemsById.TryAdd(item.ObjectId.Value, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        void func(RoomObjectId objectId) => _state.DirtyItemIds.Add(objectId.Value);

        item.SetAction(func);

        await AttatchFloorLogicIfNeededAsync(item, ct);

        if (GetTileIdForFloorItem(item, out var tileIds))
        {
            foreach (var id in tileIds)
            {
                _state.TileFloorStacks[id].Add(item.ObjectId.Value);

                await _roomMap.ComputeTileAsync(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.TryGetValue(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (item.X == newX && item.Y == newY && item.Rotation == newRotation)
            return false;

        if (GetTileIdForFloorItem(item, out var oldTileIds))
        {
            foreach (var id in oldTileIds)
            {
                _state.TileFloorStacks[id].Remove(item.ObjectId.Value);

                await _roomMap.ComputeTileAsync(id);
            }
        }

        var newId = _roomMap.GetTileId(newX, newY);

        item.SetPosition(newX, newY, _state.TileHeights[newId]);
        item.SetRotation(newRotation);

        if (GetTileIdForFloorItem(item, out var newTileIds))
        {
            foreach (var id in newTileIds)
            {
                _state.TileFloorStacks[id].Add(item.ObjectId.Value);

                await _roomMap.ComputeTileAsync(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.Remove(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (GetTileIdForFloorItem(item, out var tileIds))
        {
            foreach (var id in tileIds)
            {
                _state.TileFloorStacks[id].Remove(item.ObjectId.Value);

                await _roomMap.ComputeTileAsync(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(ctx.PlayerId), ct);

        item.SetAction(null);

        await item.Logic.OnDetachAsync(ct);

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
        RoomObjectId objectId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (!_state.FloorItemsById.TryGetValue(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ValidateFloorItemPlacementAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int newX,
        int newY,
        Rotation newRotation
    )
    {
        if (!_state.FloorItemsById.TryGetValue(objectId.Value, out var tItem))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var isRotating = tItem.Rotation != newRotation;

        if (
            _roomMap.GetTileIdForSize(
                newX,
                newY,
                newRotation,
                tItem.Definition.Width,
                tItem.Definition.Height,
                out var tileIds
            )
        )
        {
            foreach (var id in tileIds)
            {
                var tileFlags = _state.TileFlags[id];
                var tileHeight = _state.TileHeights[id];

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + tItem.Definition.StackHeight) > _roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.AvatarOccupied) && !isRotating
                )
                    return false;

                if (
                    _state.FloorItemsById.TryGetValue(
                        _state.TileHighestFloorItems[id],
                        out var bItem
                    )
                )
                {
                    if (isRotating && bItem == tItem)
                        continue;

                    if (tileFlags.Has(RoomTileFlags.StackBlocked))
                        return false;

                    if (bItem != tItem)
                    {
                        // if is a stack helper, allow placement
                        // if is a roller, disallow placement
                    }
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
