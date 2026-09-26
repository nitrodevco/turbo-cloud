using Turbo.Primitives.Players;

namespace Turbo.Primitives.Inventory.Snapshots;

/// <summary>A pet or a bot as the inventory holds it: which one, and whose.</summary>
public interface IInventoryUnitSnapshot
{
    public int Id { get; }
    public PlayerId OwnerId { get; }
}
