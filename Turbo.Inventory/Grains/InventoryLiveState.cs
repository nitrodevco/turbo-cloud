using System.Collections.Generic;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Inventory.Grains;

internal sealed class InventoryLiveState
{
    public Dictionary<int, IFurnitureItem> FurnitureById { get; } = [];
    public bool IsFurnitureReady { get; set; } = false;
}
