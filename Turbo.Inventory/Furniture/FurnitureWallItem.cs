using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureWallItem : FurnitureItem, IFurnitureWallItem
{
    public required IStuffData StuffData { get; init; }

    private FurnitureWallItemSnapshot? _snapshot;

    public override FurnitureItemSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    private FurnitureWallItemSnapshot BuildSnapshot() =>
        new()
        {
            OwnerId = OwnerId,
            OwnerName = string.Empty,
            ItemId = ItemId,
            SpriteId = Definition.SpriteId,
            Definition = Definition,
            StuffData = StuffData.GetSnapshot(),
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
}
