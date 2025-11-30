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
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
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

    public bool AreTilesDiagonal(int x1, int y1, int x2, int y2) =>
        Math.Abs(x2 - x1) == 1 && Math.Abs(y2 - y1) == 1;

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

    public Task ComputeTileAsync(int x, int y) => ComputeTileAsync(GetTileId(x, y));

    public Task ComputeTileAsync(int id)
    {
        var nextHeight = _state.Model?.BaseHeights[id] ?? 0.0;
        var nextFlags =
            _state.Model?.BaseFlags[id] ?? (RoomTileFlags.Disabled | RoomTileFlags.Closed);
        var floorStack = _state.TileFloorStacks[id];
        var avatarStack = _state.TileAvatarStacks[id];

        if (avatarStack.Count > 0)
            nextFlags = nextFlags.Add(RoomTileFlags.AvatarOccupied);

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

        var prevEncoded = _state.TileEncodedHeights[id];
        var nextEncoded = RoomModelCompiler.EncodeHeight(
            nextHeight,
            nextFlags.Has(RoomTileFlags.StackBlocked)
        );

        if (prevEncoded != nextEncoded)
        {
            _state.TileEncodedHeights[id] = nextEncoded;
            _state.DirtyHeightTileIds.Add(id);
        }

        _state.TileHeights[id] = nextHeight;
        _state.TileFlags[id] = nextFlags;
        _state.TileHighestFloorItems[id] = nextHighestItem?.ObjectId.Value ?? -1;

        _dirty = true;

        return Task.CompletedTask;
    }

    public RoomMapSnapshot GetMapSnapshot(CancellationToken ct)
    {
        if (_dirty || _mapSnapshot is null)
        {
            _mapSnapshot = BuildSnapshot();
            _dirty = false;
        }

        return _mapSnapshot;
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
                HighestObjectId = RoomObjectId.From((int)_state.TileHighestFloorItems[id]),
            }
        );

    private RoomMapSnapshot BuildSnapshot()
    {
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
        };
    }

    internal async Task EnsureMapBuiltAsync(CancellationToken ct)
    {
        if (_state.IsMapReady)
            return;

        var size = _state.Model?.Size ?? 0;

        var tileHeights = new double[size];
        var tileEncodedHeights = new short[size];
        var tileFlags = new RoomTileFlags[size];
        var tileHighestFloorItems = new long[size];
        var tileFloorStacks = new List<long>[size];
        var tileAvatarStacks = new List<long>[size];

        for (int id = 0; id < size; id++)
        {
            var height = _state.Model?.BaseHeights[id] ?? 0.0;
            var flags =
                _state.Model?.BaseFlags[id]
                ?? (RoomTileFlags.Disabled | RoomTileFlags.Closed | RoomTileFlags.StackBlocked);

            tileHeights[id] = height;
            tileEncodedHeights[id] = RoomModelCompiler.EncodeHeight(
                height,
                flags.Has(RoomTileFlags.StackBlocked)
            );
            tileFlags[id] = flags;
            tileHighestFloorItems[id] = -1;
            tileFloorStacks[id] = [];
            tileAvatarStacks[id] = [];
        }

        _state.TileHeights = tileHeights;
        _state.TileEncodedHeights = tileEncodedHeights;
        _state.TileFlags = tileFlags;
        _state.TileHighestFloorItems = tileHighestFloorItems;
        _state.TileFloorStacks = tileFloorStacks;
        _state.TileAvatarStacks = tileAvatarStacks;
        _state.IsMapReady = true;
    }

    internal async Task FlushDirtyHeightTileIdsAsync(CancellationToken ct)
    {
        if (_state.DirtyHeightTileIds.Count == 0)
            return;

        var dirtyHeightTileIds = _state.DirtyHeightTileIds.ToArray();

        _state.DirtyHeightTileIds.Clear();

        var dirtySnapshots = new List<RoomTileSnapshot>();

        foreach (var id in dirtyHeightTileIds)
        {
            //await ComputeTileAsync(id);

            var (x, y) = GetTileXY(id);

            dirtySnapshots.Add(
                new RoomTileSnapshot
                {
                    X = (byte)x,
                    Y = (byte)y,
                    Height = _state.TileHeights[id],
                    EncodedHeight = _state.TileEncodedHeights[id],
                    Flags = _state.TileFlags[id],
                    HighestObjectId = RoomObjectId.From((int)_state.TileHighestFloorItems[id]),
                }
            );
        }

        if (dirtySnapshots.Count == 0)
            return;

        _ = _roomGrain.SendComposerToRoomAsync(
            new HeightMapUpdateMessageComposer { Tiles = [.. dirtySnapshots] },
            ct
        );
    }
}
