using System.Collections.Generic;
using System.Collections.Immutable;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Rooms.Furniture;

namespace Turbo.Primitives.Rooms.Mapping;

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
    public double[] TileHeights { get; }
    public short[] TileRelativeHeights { get; }
    public byte[] TileStates { get; }
    public long[] TileHighestFloorItems { get; }
    public List<long>[] TileFloorStacks { get; }
    public IRoomFloorItem GetFloorItemById(long itemId);
    public List<RoomTileSnapshot> GetAndFlushPendingTileUpdates();
    public ImmutableArray<RoomFloorItemSnapshot> GetAllFloorItems();
    public bool AddFloorItem(IRoomFloorItem item);
    public bool RemoveFloorItemById(long itemId);
    public bool MoveFloorItem(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        out RoomFloorItemSnapshot snapshot
    );
}
