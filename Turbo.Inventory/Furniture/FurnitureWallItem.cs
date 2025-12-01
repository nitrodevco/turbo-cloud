using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureWallItem : FurnitureItem, IFurnitureWallItem
{
    public required string StuffData { get; init; }
}
