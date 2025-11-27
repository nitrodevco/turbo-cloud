using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms.Grains.Modules;

internal sealed class RoomMapModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;

    private RoomMapSnapshot? _mapSnapshot = null;
    private bool _dirty = true;

    public int GetTileId(int x, int y)
    {
        if (!IsTileInBounds(x, y))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        return y * (_state.Model?.Width ?? 0) + x;
    }

    public (int x, int y) GetTileXY(int id)
    {
        if (!IsTileInBounds(id))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        return (id % (_state.Model?.Width ?? 0), id / (_state.Model?.Width ?? 0));
    }

    public bool IsTileInBounds(int x, int y) =>
        x >= 0 && y >= 0 && x < (_state.Model?.Width ?? 0) && y < (_state.Model?.Height ?? 0);

    public bool IsTileInBounds(int id) => id >= 0 && id < (_state.Model?.Size ?? 0);

    public (int x, int y) GetTileInfront(int x, int y, Rotation rot) =>
        rot switch
        {
            Rotation.North => (x, y - 1),
            Rotation.NorthEast => (x + 1, y - 1),
            Rotation.East => (x + 1, y),
            Rotation.SouthEast => (x + 1, y + 1),
            Rotation.South => (x, y + 1),
            Rotation.SouthWest => (x - 1, y + 1),
            Rotation.West => (x - 1, y),
            Rotation.NorthWest => (x - 1, y - 1),
            _ => (x, y),
        };

    public int GetTileInfront(int id, Rotation rot)
    {
        var (x, y) = GetTileXY(id);

        (x, y) = GetTileInfront(x, y, rot);

        return GetTileId(x, y);
    }

    public bool GetTileIdForSize(
        int x,
        int y,
        Rotation rotation,
        int width,
        int height,
        out List<int> tileIds
    )
    {
        tileIds = [];

        if (width > 0 && height > 0)
        {
            if (rotation == Rotation.East || rotation == Rotation.West)
                (width, height) = (height, width);
        }

        for (var minX = x; minX < x + width; minX++)
        {
            for (var minY = y; minY < y + height; minY++)
                tileIds.Add(GetTileId(minX, minY));
        }

        return true;
    }

    public bool GetTileIdForFloorItem(IRoomFloorItem item, out List<int> tileIds) =>
        GetTileIdForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Height,
            out tileIds
        );

    public async Task EnsureMapBuiltAsync(CancellationToken ct)
    {
        if (_state.IsMapBuilt)
            return;

        var size = _state.Model?.Size ?? 0;

        var tileFloorStacks = new List<long>[size];

        for (int id = 0; id < size; id++)
            tileFloorStacks[id] = [];

        _state.TileHeights = new double[size];
        _state.TileEncodedHeights = new short[size];
        _state.TileFlags = new RoomTileFlags[size];
        _state.TileHighestFloorItems = new long[size];
        _state.TileFloorStacks = tileFloorStacks;
        _state.IsMapBuilt = true;
        _state.NeedsCompile = true;
    }

    public async Task EnsureMapCompiledAsync(CancellationToken ct)
    {
        if (!_state.NeedsCompile)
            return;

        _state.NeedsCompile = false;

        for (int id = 0; id < (_state.Model?.Size ?? 0); id++)
            await ComputeTileAsync(id);

        _state.DirtyTileIds.Clear();
    }

    public Task ComputeTileAsync(int x, int y) => ComputeTileAsync(GetTileId(x, y));

    public Task ComputeTileAsync(int id)
    {
        if (!_state.NeedsCompile)
        {
            var nextHeight = _state.Model?.BaseHeights[id] ?? 0.0;
            var nextFlags = _state.Model?.BaseFlags[id] ?? RoomTileFlags.Disabled;
            var floorStack = _state.TileFloorStacks[id];

            IRoomFloorItem? nextHighestItem = null;

            if (floorStack.Count > 0)
            {
                foreach (var itemId in floorStack)
                {
                    var item = _state.FloorItemsById[itemId];

                    if (item is null)
                        continue;

                    var height = item.Z + item.Logic.GetHeight();

                    // special logic if stack helper

                    if (height < nextHeight)
                        continue;

                    nextHeight = height;
                    nextHighestItem = item;
                }
            }

            nextHeight = Math.Truncate(nextHeight * 1000) / 1000;

            if (!nextFlags.Has(RoomTileFlags.Disabled))
            {
                if (nextHighestItem is not null)
                {
                    if (!nextHighestItem.Logic.CanWalk())
                    {
                        nextFlags = nextFlags.Remove(RoomTileFlags.Open);
                        nextFlags = nextFlags.Add(RoomTileFlags.Closed);
                    }

                    if (!nextHighestItem.Logic.CanStack())
                        nextFlags = nextFlags.Add(RoomTileFlags.StackBlocked);

                    if (nextHighestItem.Logic.CanSit())
                        nextFlags = nextFlags.Add(RoomTileFlags.Sittable);

                    if (nextHighestItem.Logic.CanLay())
                        nextFlags = nextFlags.Add(RoomTileFlags.Layable);
                }
            }

            var prevEncoded = _state.TileEncodedHeights[id];
            var nextEncoded = RoomModelCompiler.EncodeHeight(
                nextHeight,
                nextFlags.Has(RoomTileFlags.StackBlocked)
            );

            if (prevEncoded != nextEncoded)
                _state.DirtyTileIds.Add(id);

            _state.TileHeights[id] = nextHeight;
            _state.TileEncodedHeights[id] = nextEncoded;
            _state.TileFlags[id] = nextFlags;
            _state.TileHighestFloorItems[id] = nextHighestItem?.Id ?? -1;

            _dirty = true;
        }

        return Task.CompletedTask;
    }

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        GetTileSnapshotAsync(GetTileId(x, y), ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        Task.FromResult(
            new RoomTileSnapshot
            {
                X = (byte)(id % (_state.Model?.Width ?? 0)),
                Y = (byte)(id / (_state.Model?.Width ?? 0)),
                Height = _state.TileHeights[id],
                EncodedHeight = _state.TileEncodedHeights[id],
                Flags = _state.TileFlags[id],
            }
        );

    public RoomMapSnapshot GetMapSnapshot(CancellationToken ct)
    {
        if (_dirty || _mapSnapshot is null)
        {
            _mapSnapshot = BuildSnapshot();
            _dirty = false;
        }

        return _mapSnapshot;
    }

    private RoomMapSnapshot BuildSnapshot()
    {
        var items = new List<RoomFloorItemSnapshot>(_state.FloorItemsById.Count);

        foreach (var stack in _state.TileFloorStacks)
        {
            for (var i = 0; i < stack.Count; i++)
                items.Add(_state.FloorItemsById[stack[i]].GetSnapshot());
        }

        return new()
        {
            ModelName = _state.Model?.Name ?? string.Empty,
            ModelData = _state.Model?.Model ?? string.Empty,
            Width = _state.Model?.Width ?? 0,
            Height = _state.Model?.Height ?? 0,
            Size = _state.Model?.Size ?? 0,
            DoorX = _state.Model?.DoorX ?? 0,
            DoorY = _state.Model?.DoorY ?? 0,
            DoorRotation = _state.Model?.DoorRotation ?? 0,
            TileEncodedHeights = [.. _state.TileEncodedHeights],
            FloorItems = [.. items],
        };
    }

    internal async Task FlushDirtyTileIdsAsync(CancellationToken ct)
    {
        if (_state.DirtyTileIds.Count == 0)
            return;

        var dirtyTileIds = _state.DirtyTileIds.ToArray();

        _state.DirtyTileIds.Clear();

        var dirtySnapshots = dirtyTileIds
            .Select(id => new RoomTileSnapshot
            {
                X = (byte)(id % (_state.Model?.Width ?? 0)),
                Y = (byte)(id / (_state.Model?.Width ?? 0)),
                Height = _state.TileHeights[id],
                EncodedHeight = _state.TileEncodedHeights[id],
                Flags = _state.TileFlags[id],
            })
            .ToArray();

        _ = _roomGrain.SendComposerToRoomAsync(
            new HeightMapUpdateMessageComposer { Tiles = [.. dirtySnapshots] },
            ct
        );
    }
}
