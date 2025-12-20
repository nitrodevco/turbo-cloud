using System;
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
    public FurnitureDefinitionSnapshot Definition { get; }
    public IStuffData StuffData { get; }
    public bool IsDirty { get; }
    public void SetAction(Action<int>? onSnapshotChanged);
    public void MarkDirty();
    public FurnitureItemSnapshot GetSnapshot();
}
