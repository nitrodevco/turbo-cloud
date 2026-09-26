using System;
using System.Collections.Generic;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains;

/// <summary>
/// What an inventory holds in memory. Each section is filled on first use and only lists what
/// is not in a room: a placed item, pet or bot belongs to its room until it is picked up.
/// </summary>
internal sealed class InventoryLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The owner's name as every snapshot carries it; fetched once per activation.</summary>
    public string? OwnerName { get; set; }

    public Dictionary<int, IFurnitureItem> FurnitureById { get; } = [];
    public bool IsFurnitureReady { get; set; } = false;

    public InventoryUnitSection<PetSnapshot> Pets { get; } = new();
    public InventoryUnitSection<BotSnapshot> Bots { get; } = new();

    // Badges are not placed anywhere, so unlike the other sections this one lists every badge.
    public Dictionary<string, InventoryBadge> BadgesByCode { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public bool IsBadgesReady { get; set; } = false;
}
