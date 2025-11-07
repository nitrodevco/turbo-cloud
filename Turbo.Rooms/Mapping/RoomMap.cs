using System;
using System.Runtime.CompilerServices;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Snapshots.Rooms.Mapping;
using Turbo.Rooms.Abstractions;

namespace Turbo.Rooms.Mapping;

public sealed class RoomMap : IRoomMap
{
    private readonly float[] _baseHeights;
    private readonly byte[] _baseStates;

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

    public long Version { get; private set; }

    public RoomMap(RoomModelSnapshot roomModelSnapshot)
    {
        _baseHeights = (float[])roomModelSnapshot.CompiledModel.Heights.Clone();
        _baseStates = (byte[])roomModelSnapshot.CompiledModel.States.Clone();

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

        Array.Copy(_baseHeights, TileHeights, Size);
        Array.Copy(_baseStates, TileStates, Size);

        for (int i = 0; i < Size; i++)
            TileRelativeHeights[i] = EncodeHeight(TileHeights[i], (TileStates[i] & 1) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Idx(int x, int y) => y * Width + x;

    public void ComputeTile(int x, int y)
    {
        var idx = Idx(x, y);
    }

    public void ComputeTileHeight(int x, int y)
    {
        var idx = Idx(x, y);
    }

    public void ComputeTileState(int x, int y)
    {
        var idx = Idx(x, y);
    }

    private static short EncodeHeight(float height, bool stackingBlocked)
    {
        if (height < 0f)
            return -1;

        int stackingMask = 1 << 14;
        int heightMask = stackingMask - 1;
        int raw = (int)MathF.Round(height * 256f);

        if (raw < 0)
            raw = 0;

        if (raw > heightMask)
            raw = heightMask;

        int value = raw | (stackingBlocked ? stackingMask : 0);

        value &= 0x7FFF;

        return unchecked((short)value);
    }
}
