using System;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureItem
{
    public int ItemId { get; }
    public int OwnerId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public bool IsDirty { get; }
    public void SetAction(Action<int>? onSnapshotChanged);
    public void MarkDirty();
}
