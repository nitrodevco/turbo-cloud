using System.Collections.Generic;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Snapshots.Inventory;

namespace Turbo.Inventory.Grains;

internal sealed class InventoryLiveState
{
    public Dictionary<int, IFurnitureFloorItem> FloorItemsById { get; } = [];
}
