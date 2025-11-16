using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomFloorItem
{
    public long Id { get; }
    public long OwnerId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public IStuffData StuffData { get; init; }
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation Rotation { get; }
    public void SetPosition(int x, int y, double z);
    public void SetRotation(Rotation rotation);
}
