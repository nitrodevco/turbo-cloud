using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Inventory.Factories;

/// <summary>
/// Builds inventory furniture items. Every way an item reaches an inventory (loaded, granted,
/// picked up, traded) goes through here so they all read extra data and stuff data the same way.
/// </summary>
public interface IInventoryFurnitureLoader
{
    /// <summary>The furniture rows a player owns that are in no room.</summary>
    public Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        PlayerId playerId,
        string ownerName,
        CancellationToken ct
    );

    /// <summary>An item for a furniture row the caller has just read or inserted.</summary>
    public IFurnitureItem Create(
        RoomObjectId itemId,
        PlayerId ownerId,
        string ownerName,
        FurnitureDefinitionSnapshot definition,
        string? extraDataJson,
        DateTime? createdAtUtc
    );

    /// <summary>An item picked up from a room.</summary>
    public IFurnitureItem CreateFromRoomItemSnapshot(RoomItemSnapshot snapshot);

    /// <summary>An item arriving from another inventory (a trade), re-owned by this player.</summary>
    public IFurnitureItem CreateFromFurnitureItemSnapshot(
        FurnitureItemSnapshot snapshot,
        PlayerId ownerId,
        string ownerName
    );
}
