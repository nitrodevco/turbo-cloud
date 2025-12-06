using System;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Inventory.Furniture;

internal abstract class FurnitureItem : IFurnitureItem
{
    public required int ItemId { get; init; }
    public required int OwnerId { get; init; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    protected Action<int>? _onSnapshotChanged;
    protected bool _dirty = true;

    public bool IsDirty => _dirty;

    public void SetAction(Action<int>? onSnapshotChanged)
    {
        _onSnapshotChanged = onSnapshotChanged;
    }

    public abstract FurnitureItemSnapshot GetSnapshot();

    public void MarkDirty()
    {
        _dirty = true;
        _onSnapshotChanged?.Invoke(ItemId);
    }
}
