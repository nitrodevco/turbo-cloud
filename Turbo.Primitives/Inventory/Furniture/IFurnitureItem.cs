using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureItem
{
    public RoomObjectId ItemId { get; }
    public PlayerId OwnerId { get; }
    public string OwnerName { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public IExtraData ExtraData { get; }
    public IStuffData StuffData { get; }
    public FurnitureItemSnapshot GetSnapshot();
}
