using System;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureItem
{
    public int ItemId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public bool IsDirty { get; }
    public void SetAction(Action<int>? onSnapshotChanged);
    public void MarkDirty();
}
