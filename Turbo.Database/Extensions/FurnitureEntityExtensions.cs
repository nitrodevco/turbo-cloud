using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Database.Extensions;

public static class FurnitureEntityExtensions
{
    /// <summary>
    /// A furniture row as an item snapshot. The definition, the stuff data read from the extra
    /// data and the owner's name are not on the row and are handed in.
    /// </summary>
    public static FurnitureItemSnapshot ToItemSnapshot(
        this FurnitureEntity entity,
        FurnitureDefinitionSnapshot definition,
        StuffDataSnapshot stuffData,
        string ownerName
    ) =>
        new()
        {
            ItemId = entity.Id,
            SpriteId = definition.SpriteId,
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            OwnerName = ownerName,
            Definition = definition,
            StuffData = stuffData,
            ExtraData = entity.ExtraData ?? string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = entity.RoomEntityId is { } roomId ? RoomId.Parse(roomId) : RoomId.Invalid,
        };
}
