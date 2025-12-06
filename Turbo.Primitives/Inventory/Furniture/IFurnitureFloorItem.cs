using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureFloorItem : IFurnitureItem
{
    public IStuffData StuffData { get; }
}
