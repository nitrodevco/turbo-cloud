using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Rooms.Grains.WiredTrading;

/// <summary>One furni row in a chest, with the type the chest counts it under.</summary>
internal sealed record WiredChestStoredItem(
    IFurnitureItem Item,
    ChestItemTypeSnapshot Type,
    long TransactionId
)
{
    public ChestStorageSnapshot ToStorageSnapshot()
    {
        var snapshot = Item.GetSnapshot();

        return new()
        {
            ItemId = snapshot.ItemId,
            LockState = 0,
            TransactionId = TransactionId,
            Type = Type,
            Groupable = snapshot.Definition.CanGroup,
            SpecialType = (int)snapshot.Definition.FurniCategory,
            StuffData = snapshot.StuffData,
            Extra = snapshot.Extra,
        };
    }
}
