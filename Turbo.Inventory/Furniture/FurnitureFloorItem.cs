using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

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
            ItemId = ItemId,
            SpriteId = Definition.SpriteId,
            FurniCategory = FurnitureType.Default,
            ProductType = Definition.ProductType,
            StuffData = StuffDataSnapshot.FromStuffData(StuffData),
            CanRecycle = Definition.CanRecycle,
            CanTrade = Definition.CanTrade,
            CanGroup = Definition.CanGroup,
            CanSell = Definition.CanSell,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
}
