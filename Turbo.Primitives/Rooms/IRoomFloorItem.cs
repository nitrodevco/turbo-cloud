using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms;

public interface IRoomFloorItem
{
    public long Id { get; }
    public int X { get; }
    public int Y { get; }
    public float Z { get; }
    public Rotation Rotation { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public IStuffData StuffData { get; init; }
    public void SetPosition(int x, int y, float z);
    public void SetRotation(Rotation rotation);
}
