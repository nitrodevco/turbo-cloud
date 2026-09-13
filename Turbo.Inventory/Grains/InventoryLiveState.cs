using System.Collections.Generic;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains;

internal sealed class InventoryLiveState
{
    public required PlayerId PlayerId { get; init; }
    public Dictionary<int, IFurnitureItem> FurnitureById { get; } = [];
    public bool IsFurnitureReady { get; set; } = false;
}
