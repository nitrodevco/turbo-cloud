using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureWallItem : IFurnitureItem
{
    public IStuffData StuffData { get; }
}
