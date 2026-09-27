using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Systems;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Maintains AS3 FurnitureWaterAreaVisualization neighbour masks as room items change.
/// Matching water definitions join at equal Z. Different water types retain their
/// transition artwork instead of exposing a hard edge between their base textures.
/// </summary>
public sealed class RoomWaterAreaSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener
{
    private readonly Dictionary<int, HashSet<IRoomFloorItem>> _itemsByTile = [];
    private readonly Dictionary<IRoomFloorItem, List<int>> _tilesByItem = [];
    private readonly Dictionary<RoomObjectId, IRoomFloorItem> _waterById = [];
    private readonly Dictionary<IRoomFloorItem, FloorFootprint> _footprintByItem = [];

    public async Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case RoomItemAttatchedEvent attachedEvent:
                if (TryGetWater(attachedEvent.ObjectId, out var attached))
                {
                    Index(attached);
                    await RecomputeAsync(attached, ct);
                    await RecomputeNeighborsAsync(attached, ct);
                }
                break;
            case RoomItemDetachedEvent detachedEvent:
                if (TryGetTrackedWater(detachedEvent.ObjectId, out var detached))
                {
                    var oldTiles = new List<int>(GetAffectedTiles(detached));
                    Unindex(detached);
                    await RecomputeAtTilesAsync(oldTiles, ct);
                }
                break;
            case RoomItemMovedEvent movedEvent:
                if (TryGetWater(movedEvent.ObjectId, out var movedWater))
                {
                    var oldTiles = new List<int>(GetAffectedTiles(movedWater));
                    Unindex(movedWater);
                    Index(movedWater);
                    await RecomputeAsync(movedWater, ct);
                    await RecomputeAtTilesAsync(oldTiles, ct);
                    await RecomputeAtTilesAsync(GetAffectedTiles(movedWater), ct);
                }
                break;
            case RoomItemStateChangedEvent stateEvent
                when TryGetWater(stateEvent.ObjectId, out var changed):
                await RecomputeAsync(changed, ct);
                break;
        }
    }

    private bool TryGetWater(RoomObjectId id, out IRoomFloorItem item)
    {
        if (
            _roomGrain._state.ItemsById.TryGetValue(id, out var found)
            && found is IRoomFloorItem floor
            && floor.Logic is FurnitureWaterAreaLogic
        )
        {
            item = floor;
            return true;
        }

        item = null!;
        return false;
    }

    private bool TryGetTrackedWater(RoomObjectId id, out IRoomFloorItem item) =>
        _waterById.TryGetValue(id, out item!);

    private void Index(IRoomFloorItem item)
    {
        if (_tilesByItem.ContainsKey(item))
            return;

        var tiles = new List<int>();
        foreach (var (x, y) in FloorFootprint.Of(item).Tiles())
        {
            if (!_roomGrain.MapModule.InBounds(x, y))
                continue;

            var idx = _roomGrain.MapModule.ToIdx(x, y);
            tiles.Add(idx);
            (_itemsByTile.TryGetValue(idx, out var items) ? items : (_itemsByTile[idx] = [])).Add(
                item
            );
        }

        _tilesByItem[item] = tiles;
        _waterById[item.ObjectId] = item;
        _footprintByItem[item] = FloorFootprint.Of(item);
    }

    private void Unindex(IRoomFloorItem item)
    {
        if (!_tilesByItem.Remove(item, out var tiles))
            return;

        _waterById.Remove(item.ObjectId);
        _footprintByItem.Remove(item);

        foreach (var idx in tiles)
            if (
                _itemsByTile.TryGetValue(idx, out var items)
                && (items.Remove(item) && items.Count == 0)
            )
                _itemsByTile.Remove(idx);
    }

    private async Task RecomputeNeighborsAsync(IRoomFloorItem item, CancellationToken ct)
    {
        await RecomputeAtTilesAsync(GetAffectedTiles(item), ct);
    }

    private async Task RecomputeAtTilesAsync(IEnumerable<int> tileIds, CancellationToken ct)
    {
        var seen = new HashSet<IRoomFloorItem>();
        foreach (var idx in tileIds)
            if (_itemsByTile.TryGetValue(idx, out var items))
                foreach (var candidate in items)
                    if (seen.Add(candidate))
                        await RecomputeAsync(candidate, ct);
    }

    private IEnumerable<int> GetRingTiles(IRoomFloorItem item)
    {
        return GetRingTiles(_footprintByItem[item]);
    }

    private IEnumerable<int> GetRingTiles(FloorFootprint footprint)
    {
        for (var x = footprint.X - 1; x <= footprint.X + footprint.Width; x++)
        {
            if (_roomGrain.MapModule.InBounds(x, footprint.Y - 1))
                yield return _roomGrain.MapModule.ToIdx(x, footprint.Y - 1);
            if (_roomGrain.MapModule.InBounds(x, footprint.Y + footprint.Length))
                yield return _roomGrain.MapModule.ToIdx(x, footprint.Y + footprint.Length);
        }

        for (var y = footprint.Y; y < footprint.Y + footprint.Length; y++)
        {
            if (_roomGrain.MapModule.InBounds(footprint.X - 1, y))
                yield return _roomGrain.MapModule.ToIdx(footprint.X - 1, y);
            if (_roomGrain.MapModule.InBounds(footprint.X + footprint.Width, y))
                yield return _roomGrain.MapModule.ToIdx(footprint.X + footprint.Width, y);
        }
    }

    private IEnumerable<int> GetAffectedTiles(IRoomFloorItem item)
    {
        foreach (var idx in _tilesByItem[item])
            yield return idx;

        foreach (var idx in GetRingTiles(_footprintByItem[item]))
            yield return idx;
    }

    private async Task RecomputeAsync(IRoomFloorItem item, CancellationToken ct)
    {
        if (item.Logic is not FurnitureWaterAreaLogic water)
            return;

        var footprint = FloorFootprint.Of(item);
        var mask = WaterAreaMask.Build(
            footprint.Width,
            footprint.Length,
            (x, y) => IsWaterAt(item, x + footprint.X, y + footprint.Y)
        );

        var old = water.GetState();
        water.SetDerivedState(mask);
        if (old != mask && _roomGrain._roomOutbound is not null)
            await item.Logic.Context.RefreshStuffDataAsync();
    }

    private bool IsWaterAt(IRoomFloorItem source, int x, int y)
    {
        if (!_roomGrain.MapModule.InBounds(x, y))
            return false;

        var idx = _roomGrain.MapModule.ToIdx(x, y);
        if (!_itemsByTile.TryGetValue(idx, out var items))
            return false;

        foreach (var item in items)
            if (
                !ReferenceEquals(item, source)
                && item.Logic is FurnitureWaterAreaLogic
                && item.Definition.Id == source.Definition.Id
                && item.Z == source.Z
            )
                return true;

        return false;
    }
}
