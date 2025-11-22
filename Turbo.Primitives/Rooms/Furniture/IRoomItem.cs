using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomItem
{
    public long Id { get; }
    public long OwnerId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }

    public IStuffData StuffData { get; }
    public string GetStuffDataRaw();
    public void SetStuffDataRaw(string stuffDataRaw);
    public void SetStuffData(IStuffData stuffData);
}
