using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms.Grains.Modules;

public sealed class RoomMapModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;

    private RoomMapSnapshot? _mapSnapshot = null;
    private int _mapVersion = 0;

    public Task OnActivateAsync(CancellationToken ct) => Task.CompletedTask;

    public Task OnDeactivateAsync(CancellationToken ct) => Task.CompletedTask;

    public int GetTileId(int x, int y)
    {
        var id = y * (_state.Model?.Width ?? 0) + x;

        if (id < 0 || id >= (_state.Model?.Size ?? 0))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        return id;
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
        _state.TileRelativeHeights = new short[size];
        _state.TileStates = new byte[size];
        _state.TileHighestFloorItems = new long[size];
        _state.TileFloorStacks = tileFloorStacks;
        _state.IsMapBuilt = true;
    }

    public Task EnsureMapCompiledAsync(CancellationToken ct)
    {
        if (_state.NeedsCompile)
        {
            _state.NeedsCompile = false;

            for (int id = 0; id < (_state.Model?.Size ?? 0); id++)
                ComputeTile(id);

            _state.DirtyTileIds.Clear();
        }

        return Task.CompletedTask;
    }

    public void ComputeTile(int id)
    {
        if (_state.NeedsCompile)
            return;

        var nextHeight = _state.Model?.Heights[id] ?? 0.0;
        var nextState = _state.Model?.States[id] ?? (byte)RoomTileStateType.Closed;
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

        var prevRelative = _state.TileRelativeHeights[id];
        var nextRelative = RoomModelCompiler.EncodeHeight(
            nextHeight,
            nextState == (byte)RoomTileStateType.Closed
        );

        if (prevRelative != nextRelative)
        {
            if (!_state.DirtyTileIds.Contains(id))
                _state.DirtyTileIds.Add(id);
        }

        _state.TileHeights[id] = nextHeight;
        _state.TileRelativeHeights[id] = nextRelative;
        _state.TileStates[id] = nextState;
        _state.TileHighestFloorItems[id] = nextHighestItem?.Id ?? -1;

        _mapVersion++;
    }

    public async Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct)
    {
        if (_mapSnapshot is not null && _mapSnapshot.Version == _mapVersion)
            return _mapSnapshot;

        var items = new List<RoomFloorItemSnapshot>(_state.FloorItemsById.Count);

        foreach (var stack in _state.TileFloorStacks)
        {
            for (var i = 0; i < stack.Count; i++)
                items.Add(RoomFloorItemSnapshot.FromFloorItem(_state.FloorItemsById[stack[i]]));
        }

        _mapSnapshot = new RoomMapSnapshot
        {
            ModelName = _state.Model?.Name ?? string.Empty,
            ModelData = _state.Model?.Model ?? string.Empty,
            Width = _state.Model?.Width ?? 0,
            Height = _state.Model?.Height ?? 0,
            Size = _state.Model?.Size ?? 0,
            DoorX = _state.Model?.DoorX ?? 0,
            DoorY = _state.Model?.DoorY ?? 0,
            DoorRotation = _state.Model?.DoorRotation ?? 0,
            TileRelativeHeights = [.. _state.TileRelativeHeights],
            FloorItems = items,
            Version = _mapVersion,
        };

        return _mapSnapshot;
    }

    public async Task FlushDirtyTileIdsAsync(CancellationToken ct)
    {
        if (_state.DirtyTileIds.Count == 0)
            return;

        var dirtyTileIds = _state.DirtyTileIds.ToArray();

        _state.DirtyTileIds.Clear();

        var dirtySnapshots = dirtyTileIds
            .Select(id => new RoomTileSnapshot
            {
                X = (byte)(id % _state.Model?.Width ?? 0),
                Y = (byte)(id / _state.Model?.Width ?? 0),
                RelativeHeight = _state.TileRelativeHeights[id],
            })
            .ToArray();

        _ = _roomGrain.SendComposerToRoomAsync(
            new HeightMapUpdateMessageComposer { Tiles = [.. dirtySnapshots] },
            ct
        );
    }
}
