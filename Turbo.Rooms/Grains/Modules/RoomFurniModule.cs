using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Enums;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed class RoomFurniModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule,
    IRoomItemsLoader itemsLoader,
    IFurnitureLogicFactory furnitureLogicFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IRoomItemsLoader _itemsLoader = itemsLoader;
    private readonly IFurnitureLogicFactory _furnitureLogicFactory = furnitureLogicFactory;

    public async Task OnActivateAsync(CancellationToken ct) { }

    public async Task OnDeactivateAsync(CancellationToken ct) { }

    public async Task EnsureFurniLoadedAsync(CancellationToken ct)
    {
        if (_state.IsFurniLoaded)
            return;

        var floorItems = await _itemsLoader.LoadByRoomIdAsync(_roomGrain.GetPrimaryKeyLong(), ct);

        foreach (var item in floorItems)
            await AddFloorItemAsync(item, ct);

        _state.IsFurniLoaded = true;
    }

    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (!_state.FloorItemsById.TryAdd(item.Id, item))
            return false;

        await AttatchFloorLogicIfNeededAsync(item, ct);

        if (_roomMap.GetTileIdForFloorItem(item, out var tileIds))
        {
            foreach (var id in tileIds)
            {
                _state.TileFloorStacks[id].Add(item.Id);

                _roomMap.ComputeTile(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(
            new ObjectAddMessageComposer
            {
                FloorItem = RoomFloorItemSnapshot.FromFloorItem(item),
                OwnerName = string.Empty,
            },
            ct
        );

        return true;
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        long itemId,
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

        if (_roomMap.GetTileIdForFloorItem(item, out var oldTileIds))
        {
            foreach (var idx in oldTileIds)
            {
                _state.TileFloorStacks[idx].Remove(item.Id);

                _roomMap.ComputeTile(idx);
            }
        }

        var newId = _roomMap.GetTileId(newX, newY);

        item.SetPosition(newX, newY, _state.TileHeights[newId]);
        item.SetRotation(newRotation);

        if (_roomMap.GetTileIdForFloorItem(item, out var newTileIds))
        {
            foreach (var id in newTileIds)
            {
                _state.TileFloorStacks[id].Add(item.Id);

                _roomMap.ComputeTile(id);
            }
        }

        if (!_state.DirtyItemIds.Contains(item.Id))
            _state.DirtyItemIds.Add(item.Id);

        _ = _roomGrain.SendComposerToRoomAsync(
            new ObjectUpdateMessageComposer
            {
                FloorItem = RoomFloorItemSnapshot.FromFloorItem(item),
            },
            ct
        );

        await item.Logic.OnMoveAsync(ct);

        return true;
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
        long itemId,
        long pickerId,
        CancellationToken ct
    )
    {
        if (!_state.FloorItemsById.Remove(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (_roomMap.GetTileIdForFloorItem(item, out var tileIds))
        {
            foreach (var id in tileIds)
            {
                _state.TileFloorStacks[id].Remove(item.Id);

                _roomMap.ComputeTile(id);
            }
        }

        _ = _roomGrain.SendComposerToRoomAsync(
            new ObjectRemoveMessageComposer
            {
                ObjectId = (int)item.Id,
                IsExpired = false,
                PickerId = (int)pickerId,
                Delay = 0,
            },
            ct
        );

        return true;
    }

    public async Task<bool> ValidateFloorPlacementAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var isRotating = item.Rotation != newRotation;

        if (
            _roomMap.GetTileIdForSize(
                newX,
                newY,
                newRotation,
                item.Definition.Width,
                item.Definition.Height,
                out var tileIds
            )
        )
        {
            foreach (var id in tileIds)
            {
                var tileState = _state.TileStates[id];
                var tileHeight = _state.TileHeights[id];

                if (
                    (tileHeight + item.Definition.StackHeight) > _roomConfig.MaxStackHeight
                    || tileState == (byte)RoomTileStateType.Closed
                )
                {
                    return false;
                }

                if (
                    _state.FloorItemsById.TryGetValue(
                        _state.TileHighestFloorItems[id],
                        out var tileItem
                    )
                )
                {
                    if (isRotating && tileItem == item)
                        continue;

                    if (tileItem != item)
                    {
                        // if is a stack helper, allow placement
                        // if is a roller, disallow placement

                        if (
                            !tileItem.Definition.CanStack
                            || tileItem.Definition.CanSit
                            || tileItem.Definition.CanLay
                        )
                            return false;
                    }
                }
            }
        }

        return true;
    }

    private async Task AttatchFloorLogicIfNeededAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (item.Logic is not null)
            return;

        var logicType = item.Definition.LogicName;
        var ctx = new RoomFloorItemContext(_roomGrain, this, item);
        var logic = _furnitureLogicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IFurnitureFloorLogic floorLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidFloorLogic);

        item.SetLogic(floorLogic);
    }

    internal async Task FlushDirtyItemIdsAsync(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        CancellationToken ct
    )
    {
        if (_state.DirtyItemIds.Count == 0)
            return;

        var dirtyItemIds = _state.DirtyItemIds.ToArray();

        _state.DirtyItemIds.Clear();

        var dbCtx = await dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var dirtySnapshots = dirtyItemIds
                .Select(id => RoomFloorItemSnapshot.FromFloorItem(_state.FloorItemsById[id]))
                .ToArray();
            var dirtyIds = dirtySnapshots.Select(x => x.Id).ToArray();
            var entities = await dbCtx
                .Furnitures.Where(x => dirtyIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var snapshot in dirtySnapshots)
            {
                if (!entities.TryGetValue((int)snapshot.Id, out var entity))
                    continue;

                entity.X = snapshot.X;
                entity.Y = snapshot.Y;
                entity.Z = snapshot.Z;
                entity.Rotation = snapshot.Rotation;
                entity.StuffData = snapshot.StuffDataJson;
            }

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }
}
