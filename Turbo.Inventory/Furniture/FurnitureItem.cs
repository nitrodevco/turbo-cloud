using System;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Sound;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureItem : IFurnitureItem
{
    public required RoomObjectId ItemId { get; init; }
    public required PlayerId OwnerId { get; init; }
    public required string OwnerName { get; init; } = string.Empty;
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public required IExtraData ExtraData { get; init; }
    public required IStuffData StuffData { get; init; }
    public DateTime? CreatedAtUtc { get; init; }

    private FurnitureItemSnapshot? _snapshot;

    public FurnitureItemSnapshot GetSnapshot()
    {
        if (_snapshot is null)
        {
            _snapshot = BuildSnapshot();
        }

        return _snapshot;
    }

    private FurnitureItemSnapshot BuildSnapshot() =>
        new()
        {
            ItemId = ItemId,
            SpriteId = Definition.SpriteId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            Definition = Definition,
            StuffData = StuffData.GetSnapshot(),
            ExtraData = ExtraData.GetJsonString(),
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
            // The client reads a song disk's song from here, never from its stuff data.
            Extra = SongDisks.IsSongDisk(Definition) ? SongDisks.SongIdOf(StuffData) : 0,
            CreatedAtUtc = CreatedAtUtc,
        };
}
