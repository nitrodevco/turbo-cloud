using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Rooms.Abstractions;

public interface IRoomMap
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
}
