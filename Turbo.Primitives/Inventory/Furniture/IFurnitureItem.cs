using System;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureItem
{
    public int ItemId { get; }
    public int OwnerId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public IStuffData StuffData { get; }
    public bool IsDirty { get; }
    public void SetAction(Action<int>? onSnapshotChanged);
    public void MarkDirty();
    public FurnitureItemSnapshot GetSnapshot();
}
