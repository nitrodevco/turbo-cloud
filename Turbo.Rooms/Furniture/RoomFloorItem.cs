using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Rooms.Furniture;

public sealed class RoomFloorItem : IRoomFloorItem
{
    public long Id { get; init; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public float Z { get; private set; }
    public Rotation Rotation { get; private set; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public required IStuffData StuffData { get; init; }

    public void SetPosition(int x, int y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public void SetRotation(Rotation rotation)
    {
        Rotation = rotation;
    }
}
