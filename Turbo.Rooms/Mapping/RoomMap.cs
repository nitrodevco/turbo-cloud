using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Rooms.Mapping;

public sealed class RoomMap : IRoomMap
{
    public string ModelName { get; }
    public string ModelData { get; }
    public int Width { get; }
    public int Height { get; }
    public int Size { get; }
    public int DoorX { get; }
    public int DoorY { get; }
    public Rotation DoorRotation { get; }

    public double[] TileHeights { get; }
    public short[] TileRelativeHeights { get; }
    public byte[] TileStates { get; }
    public long[] TileHighestFloorItems { get; }
    public List<long>[] TileFloorStacks { get; }

    private readonly double[] _baseHeights;
    private readonly byte[] _baseStates;
    private readonly Dictionary<long, IRoomFloorItem> _floorItemsById = [];

    public RoomMap(RoomModelSnapshot roomModelSnapshot)
    {
        ModelName = roomModelSnapshot.Name;
        ModelData = roomModelSnapshot.Model;
        Width = roomModelSnapshot.CompiledModel.Width;
        Height = roomModelSnapshot.CompiledModel.Height;
        Size = Width * Height;
        DoorX = roomModelSnapshot.DoorX;
        DoorY = roomModelSnapshot.DoorY;
        DoorRotation = roomModelSnapshot.DoorRotation;

        TileHeights = new double[Size];
        TileRelativeHeights = new short[Size];
        TileStates = new byte[Size];
        TileHighestFloorItems = new long[Size];
        TileFloorStacks = new List<long>[Size];
        _baseHeights = (double[])roomModelSnapshot.CompiledModel.Heights.Clone();
        _baseStates = (byte[])roomModelSnapshot.CompiledModel.States.Clone();

        Array.Copy(_baseHeights, TileHeights, Size);
        Array.Copy(_baseStates, TileStates, Size);

        BuildMap();
    }

    public IRoomFloorItem? GetFloorItemById(long itemId) =>
        _floorItemsById.TryGetValue(itemId, out var item) ? item : null;

    public ImmutableArray<RoomFloorItemSnapshot> GetAllFloorItems()
    {
        var items = new List<RoomFloorItemSnapshot>(_floorItemsById.Count);

        foreach (var stack in TileFloorStacks)
        {
            for (var i = 0; i < stack.Count; i++)
                items.Add(RoomFloorItemSnapshot.FromFloorItem(_floorItemsById[stack[i]]));
        }

        return [.. items];
    }

    public bool AddFloorItem(IRoomFloorItem item)
    {
        if (!_floorItemsById.TryAdd(item.Id, item))
            return false;

        if (GetTileIdxForFloorItem(item, out var tileIdxs))
        {
            foreach (var idx in tileIdxs)
            {
                TileFloorStacks[idx].Add(item.Id);

                ComputeTile(idx);
            }
        }

        return true;
    }

    public bool RemoveFloorItemById(long itemId)
    {
        if (!_floorItemsById.Remove(itemId, out var item))
            return false;

        if (GetTileIdxForFloorItem(item, out var tileIdxs))
        {
            foreach (var idx in tileIdxs)
            {
                TileFloorStacks[idx].Remove(item.Id);

                ComputeTile(idx);
            }
        }

        return true;
    }

    public bool MoveFloorItem(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        out IRoomFloorItem item
    )
    {
        item = _floorItemsById[itemId];

        if (item is null)
            return false;

        if (GetTileIdxForFloorItem(item, out var oldTileIdxs))
        {
            foreach (var idx in oldTileIdxs)
            {
                TileFloorStacks[idx].Remove(item.Id);

                ComputeTile(idx);
            }
        }

        double z = TileHeights[Idx(newX, newY)];

        item.SetPosition(newX, newY, z);
        item.SetRotation(newRotation);

        if (GetTileIdxForFloorItem(item, out var newTileIdxs))
        {
            foreach (var idx in newTileIdxs)
            {
                TileFloorStacks[idx].Add(item.Id);

                ComputeTile(idx);
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Idx(int x, int y)
    {
        var idx = y * Width + x;

        if (idx < 0 || idx >= Size)
            throw new IndexOutOfRangeException($"Tile index {idx} is out of bounds.");

        return idx;
    }

    private void BuildMap()
    {
        for (int idx = 0; idx < Size; idx++)
        {
            TileFloorStacks[idx] = [];
            ComputeTileRelativeHeight(idx);
        }
    }

    private bool GetTileIdxForSize(
        int x,
        int y,
        Rotation rotation,
        int width,
        int height,
        out List<int> tileIdxs
    )
    {
        tileIdxs = [];

        if (width > 0 && height > 0)
        {
            if (rotation == Rotation.East || rotation == Rotation.West)
            {
                (width, height) = (height, width);
            }
        }

        for (var minX = x; minX < x + width; minX++)
        {
            for (var minY = y; minY < y + height; minY++)
            {
                tileIdxs.Add(Idx(minX, minY));
            }
        }

        return true;
    }

    private bool GetTileIdxForFloorItem(IRoomFloorItem item, out List<int> tileIdxs) =>
        GetTileIdxForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Height,
            out tileIdxs
        );

    private void ComputeTile(int idx)
    {
        var prevHeight = TileHeights[idx];
        var nextHeight = _baseHeights[idx];
        var floorStack = TileFloorStacks[idx];
        var prevHighestId = TileHighestFloorItems[idx];
        var nextHighestId = (long)-1;

        if (floorStack.Count > 0)
        {
            foreach (var itemId in floorStack)
            {
                var item = _floorItemsById[itemId];

                if (item is null)
                    continue;

                var height = item.Z + item.Definition.StackHeight;

                // special logic if stack helper

                if (height < nextHeight)
                    continue;

                nextHeight = Math.Truncate(height * 1000) / 1000;
                nextHighestId = itemId;
            }
        }

        if (prevHeight != nextHeight)
            TileHeights[idx] = nextHeight;

        if (prevHighestId != nextHighestId)
            TileHighestFloorItems[idx] = nextHighestId;

        ComputeTileRelativeHeight(idx);
    }

    private void ComputeTileRelativeHeight(int idx)
    {
        var height = RoomModelCompiler.EncodeHeight(
            TileHeights[idx],
            TileStates[idx] == (byte)RoomTileStateType.Closed
        );

        TileRelativeHeights[idx] = height;
    }
}
