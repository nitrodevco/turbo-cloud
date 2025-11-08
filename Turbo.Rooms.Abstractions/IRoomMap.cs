using System.Collections.Generic;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms;
using Turbo.Rooms.Abstractions.Furniture;

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
    public IReadOnlyList<IRoomFloorItem> GetAllFloorItems();
    public void AddFloorItem(IRoomFloorItem item);
    public void AddFloorItemAt(IRoomFloorItem item, int X, int Y, float Z, Rotation rotation);
    public void RemoveFloorItemById(long itemId);
}
