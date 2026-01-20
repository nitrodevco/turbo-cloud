using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private RoomMapSnapshot? _mapSnapshot = null;
    private bool _dirty = true;

    public int Width => _roomGrain._state.Model?.Width ?? 0;
    public int Height => _roomGrain._state.Model?.Height ?? 0;
    public int Size => _roomGrain._state.Model?.Size ?? 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIdx(int x, int y) => y * Width + x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetX(int idx) => idx % Width;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetY(int idx) => idx / Width;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int idx) => (uint)idx < (uint)(Width * Height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDiagonal(int idxA, int idxB)
    {
        var delta = idxB - idxA;

        if (delta != Width + 1 && delta != Width - 1 && delta != -Width + 1 && delta != -Width - 1)
            return false;

        var ax = idxA % Width;
        var bx = idxB % Width;

        return Math.Abs(ax - bx) == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int dx, int dy) GetDirectionOffset(Rotation dir)
    {
        return dir switch
        {
            Rotation.North => (0, -1),
            Rotation.NorthEast => (1, -1),
            Rotation.East => (1, 0),
            Rotation.SouthEast => (1, 1),
            Rotation.South => (0, 1),
            Rotation.SouthWest => (-1, 1),
            Rotation.West => (-1, 0),
            Rotation.NorthWest => (-1, -1),
            _ => (0, 0),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetTileInFront(int index, Rotation direction, out int nextIndex)
    {
        var x = GetX(index);
        var y = GetY(index);

        var (dx, dy) = GetDirectionOffset(direction);

        var nx = x + dx;
        var ny = y + dy;

        if (!InBounds(nx, ny))
        {
            nextIndex = -1;

            return false;
        }

        nextIndex = ToIdx(nx, ny);

        return true;
    }

    public (int x, int y) GetTileXY(int idx)
    {
        if (!InBounds(idx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        return (GetX(idx), GetY(idx));
    }

    public bool GetTileIdForSize(
        int x,
        int y,
        Rotation rot,
        int width,
        int length,
        out List<int> tileIds
    )
    {
        tileIds = [];

        if (width > 0 && length > 0)
        {
            if (rot == Rotation.East || rot == Rotation.West)
                (width, length) = (length, width);
        }

        for (var minX = x; minX < x + width; minX++)
        {
            for (var minY = y; minY < y + length; minY++)
            {
                var idx = ToIdx(minX, minY);

                if (!InBounds(idx))
                    throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

                tileIds.Add(idx);
            }
        }

        return true;
    }

    public bool AddItem(IRoomItem item)
    {
        return item switch
        {
            IRoomFloorItem floor => AddFloorItem(floor),
            IRoomWallItem wall => AddWallItem(wall),
            _ => false,
        };
    }

    public bool RemoveItem(IRoomItem item)
    {
        return item switch
        {
            IRoomFloorItem floor => RemoveFloorItem(floor),
            IRoomWallItem wall => RemoveWallItem(wall),
            _ => false,
        };
    }

    public void ComputeAllTiles()
    {
        for (int idx = 0; idx < Size; idx++)
            ComputeTile(idx);
    }

    public void ComputeTile(int x, int y) => ComputeTile(ToIdx(x, y));

    public void ComputeTile(int id)
    {
        if (_roomGrain._state.IsTileComputationPaused)
            return;

        var nextHeight = _roomGrain._state.Model?.BaseHeights[id] ?? 0.0;
        var nextFlags =
            _roomGrain._state.Model?.BaseFlags[id]
            ?? (RoomTileFlags.Disabled | RoomTileFlags.Closed);
        var floorStack = _roomGrain._state.TileFloorStacks[id];
        var avatarStack = _roomGrain._state.TileAvatarStacks[id];

        IRoomFloorItem? nextHighestItem = null;

        if (avatarStack.Count > 0)
        {
            nextFlags = nextFlags.Add(RoomTileFlags.AvatarOccupied);

            foreach (var objectId in avatarStack)
            {
                if (!_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar))
                    continue;

                avatar.NeedsInvoke = true;
            }
        }

        if (floorStack.Count > 0)
        {
            nextFlags = nextFlags.Add(RoomTileFlags.FurnitureOccupied);

            foreach (var itemId in floorStack)
            {
                if (!_roomGrain._state.FloorItemsById.TryGetValue(itemId, out var item))
                    continue;

                var height = item.Z + item.GetStackHeight();

                // special logic if stack helper

                if (height < nextHeight)
                    continue;

                nextHeight = height;
                nextHighestItem = item;
            }
        }

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

        _roomGrain._state.TileHeights[id] = nextHeight;
        _roomGrain._state.TileFlags[id] = nextFlags;
        _roomGrain._state.TileHighestFloorItems[id] = nextHighestItem?.ObjectId ?? -1;

        var prevEncoded = _roomGrain._state.TileEncodedHeights[id];
        var nextEncoded = EncodeHeight(nextHeight, nextFlags.Has(RoomTileFlags.StackBlocked));

        if (prevEncoded != nextEncoded)
        {
            _roomGrain._state.TileEncodedHeights[id] = nextEncoded;
            _roomGrain._state.DirtyHeightTileIds.Add(id);
        }

        _dirty = true;
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
        GetTileSnapshotAsync(ToIdx(x, y), ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        Task.FromResult(
            new RoomTileSnapshot
            {
                X = (byte)(id % (_roomGrain._state.Model?.Width ?? 0)),
                Y = (byte)(id / (_roomGrain._state.Model?.Width ?? 0)),
                Height = _roomGrain._state.TileHeights[id],
                EncodedHeight = _roomGrain._state.TileEncodedHeights[id],
                Flags = _roomGrain._state.TileFlags[id],
                HighestObjectId = _roomGrain._state.TileHighestFloorItems[id],
                FloorObjectIds = [.. _roomGrain._state.TileFloorStacks[id]],
                AvatarObjectIds = [.. _roomGrain._state.TileAvatarStacks[id]],
            }
        );

    private RoomMapSnapshot BuildSnapshot()
    {
        return new()
        {
            ModelName = _roomGrain._state.Model?.Name ?? string.Empty,
            ModelData = _roomGrain._state.Model?.Model ?? string.Empty,
            Width = _roomGrain._state.Model?.Width ?? 0,
            Height = _roomGrain._state.Model?.Height ?? 0,
            Size = _roomGrain._state.Model?.Size ?? 0,
            DoorX = _roomGrain._state.Model?.DoorX ?? 0,
            DoorY = _roomGrain._state.Model?.DoorY ?? 0,
            DoorRotation = _roomGrain._state.Model?.DoorRotation ?? 0,
            TileEncodedHeights = [.. _roomGrain._state.TileEncodedHeights],
        };
    }

    private static short EncodeHeight(double height, bool stackingBlocked)
    {
        if (height < 0 || stackingBlocked)
            return -1;

        int stackingMask = 1 << 14;
        int heightMask = stackingMask - 1;
        int raw = (int)Math.Round(height * 256.0);

        if (raw < 0)
            raw = 0;

        if (raw > heightMask)
            raw = heightMask;

        int value = raw;

        value &= 0x7FFF;

        return unchecked((short)value);
    }

    internal Task EnsureMapBuiltAsync(CancellationToken ct)
    {
        if (!_roomGrain._state.IsMapReady)
        {
            var size = _roomGrain._state.Model?.Size ?? 0;

            var tileHeights = new double[size];
            var tileEncodedHeights = new short[size];
            var tileFlags = new RoomTileFlags[size];
            var tileHighestFloorItems = new RoomObjectId[size];
            var tileFloorStacks = new HashSet<RoomObjectId>[size];
            var tileAvatarStacks = new HashSet<RoomObjectId>[size];

            for (int id = 0; id < size; id++)
            {
                var height = _roomGrain._state.Model?.BaseHeights[id] ?? 0.0;
                var flags =
                    _roomGrain._state.Model?.BaseFlags[id]
                    ?? (RoomTileFlags.Disabled | RoomTileFlags.Closed | RoomTileFlags.StackBlocked);

                tileHeights[id] = height;
                tileEncodedHeights[id] = EncodeHeight(
                    height,
                    flags.Has(RoomTileFlags.StackBlocked)
                );
                tileFlags[id] = flags;
                tileHighestFloorItems[id] = -1;
                tileFloorStacks[id] = [];
                tileAvatarStacks[id] = [];
            }

            _roomGrain._state.TileHeights = tileHeights;
            _roomGrain._state.TileEncodedHeights = tileEncodedHeights;
            _roomGrain._state.TileFlags = tileFlags;
            _roomGrain._state.TileHighestFloorItems = tileHighestFloorItems;
            _roomGrain._state.TileFloorStacks = tileFloorStacks;
            _roomGrain._state.TileAvatarStacks = tileAvatarStacks;
            _roomGrain._state.IsMapReady = true;
        }

        return Task.CompletedTask;
    }
}
