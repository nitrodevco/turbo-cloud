using System.Collections.Generic;

namespace Turbo.Inventory.Grains;

/// <summary>
/// One section of pets or bots in memory: filled on first use, listed by id (the order the
/// client shows them in, so it is kept sorted rather than sorted per read).
/// </summary>
internal sealed class InventoryUnitSection<TSnapshot>
{
    public SortedDictionary<int, TSnapshot> ById { get; } = [];
    public bool IsReady { get; set; } = false;
}
