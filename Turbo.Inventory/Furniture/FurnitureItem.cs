using System;
using System.Text.Json;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureItem : IFurnitureItem
{
    public required RoomObjectId ItemId { get; init; }
    public required PlayerId OwnerId { get; init; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public required IStuffData StuffData { get; init; }

    private bool _dirty = true;
    private Action<int>? _onSnapshotChanged;
    private FurnitureItemSnapshot? _snapshot;

    public bool IsDirty => _dirty;

    public void SetAction(Action<int>? onSnapshotChanged)
    {
        _onSnapshotChanged = onSnapshotChanged;
    }

    public void MarkDirty()
    {
        _dirty = true;
        _onSnapshotChanged?.Invoke(ItemId);
    }

    public FurnitureItemSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    private FurnitureItemSnapshot BuildSnapshot() =>
        new()
        {
            ItemId = ItemId,
            SpriteId = Definition.SpriteId,
            OwnerId = OwnerId,
            Definition = Definition,
            StuffData = StuffData.GetSnapshot(),
            StuffDataJson = JsonSerializer.Serialize(StuffData, StuffData.GetType()),
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
}
