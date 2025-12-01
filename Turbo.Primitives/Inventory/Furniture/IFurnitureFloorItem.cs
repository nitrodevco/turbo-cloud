using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureFloorItem : IFurnitureItem
{
    public IStuffData StuffData { get; }

    public FurnitureFloorItemSnapshot GetSnapshot();
}
