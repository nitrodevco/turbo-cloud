using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureFloorItem : FurnitureItem, IFurnitureFloorItem
{
    public required IStuffData StuffData { get; init; }

    private FurnitureFloorItemSnapshot? _snapshot;

    public FurnitureFloorItemSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    private FurnitureFloorItemSnapshot BuildSnapshot() =>
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
