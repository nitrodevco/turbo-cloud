using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule
{
    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        var tileIdx = _roomGrain.MapModule.ToIdx(x, y);

        if (!_roomGrain.MapModule.InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        if (
            !await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct)
            || !_roomGrain.MapModule.PlaceFloorItem(item, tileIdx, rot)
        )
            return false;

        await item.Logic.OnPlaceAsync(ctx, ct);

        item.MarkDirty();

        await _roomGrain.SendComposerToRoomAsync(item.GetAddComposer());

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
        if (
            !_roomGrain._state.ItemsById.TryGetValue(itemId, out var item)
            || item is not IRoomFloorItem floor
        )
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var prevIdx = _roomGrain.MapModule.ToIdx(item.X, item.Y);
        var nextIdx = _roomGrain.MapModule.ToIdx(x, y);

        if (!_roomGrain.MapModule.MoveFloorItem(floor, nextIdx, rot))
            return false;

        await _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer());

        await item.Logic.OnMoveAsync(ctx, prevIdx, ct);

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
        if (
            !_roomGrain._state.ItemsById.TryGetValue(itemId, out var item)
            || item is not IRoomFloorItem tItem
        )
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (
            _roomGrain.MapModule.GetTileIdForSize(
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
                var tileFlags = _roomGrain._state.TileFlags[idx];
                var tileHeight = _roomGrain._state.TileHeights[idx];
                var highestItemId = _roomGrain._state.TileHighestFloorItems[idx];
                var isRotating = false;

                if (_roomGrain._state.FloorItemsById.TryGetValue(highestItemId, out var bItem))
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
                    || (tileHeight + tItem.GetStackHeight()) > _roomGrain._roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked) && bItem != tItem
                    || (
                        !_roomGrain._roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                        && !isRotating
                    )
                    || (tileFlags.Has(RoomTileFlags.AvatarOccupied) && !tItem.Logic.CanWalk())
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
            _roomGrain.MapModule.GetTileIdForSize(
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
                var tileFlags = _roomGrain._state.TileFlags[id];
                var tileHeight = _roomGrain._state.TileHeights[id];
                var highestItemId = _roomGrain._state.TileHighestFloorItems[id];
                IRoomFloorItem? bItem = null;

                if (_roomGrain._state.FloorItemsById.TryGetValue(highestItemId, out var floorItem))
                    bItem = floorItem;

                if (
                    tileFlags.Has(RoomTileFlags.Disabled)
                    || (tileHeight + item.GetStackHeight()) > _roomGrain._roomConfig.MaxStackHeight
                    || tileFlags.Has(RoomTileFlags.StackBlocked)
                    || (
                        !_roomGrain._roomConfig.PlaceItemsOnAvatars
                        && tileFlags.Has(RoomTileFlags.AvatarOccupied)
                    )
                    || (tileFlags.Has(RoomTileFlags.AvatarOccupied) && !item.Logic.CanWalk())
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
            _roomGrain
                ._state.ItemsById.Values.OfType<IRoomFloorItem>()
                .Select(x => x.GetSnapshot())
                .ToImmutableArray()
        );

    public bool GetTileIdForFloorItem(IRoomFloorItem item, out List<int> tileIds) =>
        _roomGrain.MapModule.GetTileIdForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Length,
            out tileIds
        );
}
