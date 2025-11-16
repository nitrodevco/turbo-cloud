using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Rooms.Furniture;

public sealed class RoomFloorItem : IRoomFloorItem
{
    public required long Id { get; init; }
    public required long OwnerId { get; init; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public required IStuffData StuffData { get; init; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public double Z { get; private set; }
    public Rotation Rotation { get; private set; }

    public void SetPosition(int x, int y, double z)
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
