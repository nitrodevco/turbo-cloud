using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;
using Turbo.Rooms.Abstractions;

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

    public float[] TileHeights { get; }
    public short[] TileRelativeHeights { get; }
    public byte[] TileStates { get; }
    public long[] TileHighestFloorItems { get; }
    public List<long>[] TileFloorStacks { get; }

    private readonly float[] _baseHeights;
    private readonly byte[] _baseStates;
    private readonly Dictionary<long, IRoomFloorItem> _floorItemsById = [];
    private readonly Dictionary<long, int> _floorItemsByTileIdx = [];

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
        TileHeights = new float[Size];
        TileRelativeHeights = new short[Size];
        TileStates = new byte[Size];
        TileHighestFloorItems = new long[Size];
        TileFloorStacks = new List<long>[Size];

        _baseHeights = (float[])roomModelSnapshot.CompiledModel.Heights.Clone();
        _baseStates = (byte[])roomModelSnapshot.CompiledModel.States.Clone();

        Array.Copy(_baseHeights, TileHeights, Size);
        Array.Copy(_baseStates, TileStates, Size);

        BuildMap();
    }

    public IReadOnlyList<IRoomFloorItem> GetAllFloorItems()
    {
        var ordered = new List<IRoomFloorItem>(_floorItemsById.Count);

        for (int idx = 0; idx < Size; idx++)
        {
            var stack = TileFloorStacks[idx];

            if (stack.Count is 0)
                continue;

            for (var i = 0; i < stack.Count; i++)
                ordered.Add(_floorItemsById[stack[i]]);
        }

        return ordered;
    }

    public void AddFloorItem(IRoomFloorItem item)
    {
        try
        {
            var idx = Idx(item.X, item.Y);

            _floorItemsById.Add(item.Id, item);
            _floorItemsByTileIdx.Add(item.Id, idx);
            TileFloorStacks[idx].Add(item.Id);

            ComputeTile(idx);
        }
        catch (Exception e)
        {
            //
        }
    }

    public void AddFloorItemAt(IRoomFloorItem item, int X, int Y, float Z, Rotation rotation)
    {
        try
        {
            var idx = Idx(X, Y);

            _floorItemsById.Add(item.Id, item);
            _floorItemsByTileIdx.Add(item.Id, idx);
            TileFloorStacks[idx].Add(item.Id);

            item.SetPosition(X, Y, Z);
            item.SetRotation(rotation);

            ComputeTile(idx);
        }
        catch (Exception e)
        {
            //
        }
    }

    public void RemoveFloorItemById(long itemId)
    {
        try
        {
            if (!_floorItemsById.TryGetValue(itemId, out var item))
                return;

            var idx = _floorItemsByTileIdx[itemId];

            _floorItemsById.Remove(itemId);
            _floorItemsByTileIdx.Remove(itemId);
            TileFloorStacks[idx].Remove(itemId);

            ComputeTile(idx);
        }
        catch (Exception e)
        {
            //
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Idx(int x, int y)
    {
        var idx = y * Width + x;

        if (idx < 0 || idx >= Size)
            throw new IndexOutOfRangeException($"Tile index {idx} is out of bounds.");

        return idx;
    }

    public void ComputeTile(int x, int y)
    {
        ComputeTile(Idx(x, y));
    }

    public void ComputeTile(int idx)
    {
        ComputeTileHeight(idx);
        ComputeTileState(idx);
    }

    public void ComputeTileState(int idx) { }

    public void ComputeTileHeight(int idx)
    {
        var prevHeight = TileHeights[idx];
        var nextHeight = _baseHeights[idx];
        var floorStack = TileFloorStacks[idx];
        var highestId = (long)-1;

        if (floorStack.Count > 0)
        {
            foreach (var itemId in floorStack)
            {
                var item = _floorItemsById[itemId];

                if (item is null)
                    continue;

                var height = item.Z; // + stackheight / def height

                // special logic if stack helper

                if (height < nextHeight)
                    continue;

                highestId = itemId;
                nextHeight = MathF.Round(height, 3);
            }
        }

        if (prevHeight != nextHeight)
            return;

        TileHeights[idx] = nextHeight;
        TileHighestFloorItems[idx] = highestId;
        ComputeTileRelativeHeight(idx);
    }

    private void BuildMap()
    {
        for (int idx = 0; idx < Size; idx++)
        {
            TileFloorStacks[idx] = [];
            ComputeTileRelativeHeight(idx);
        }
    }

    private void ComputeTileRelativeHeight(int idx)
    {
        TileRelativeHeights[idx] = RoomModelCompiler.EncodeHeight(
            TileHeights[idx],
            (TileStates[idx] & 1) != 0
        );
    }
}
