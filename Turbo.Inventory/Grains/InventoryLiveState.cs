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

    // Pets and bots are listed by id, so they are kept in that order rather than sorted per read.
    public SortedDictionary<int, PetSnapshot> PetsById { get; } = [];
    public bool IsPetsReady { get; set; } = false;

    public SortedDictionary<int, BotSnapshot> BotsById { get; } = [];
    public bool IsBotsReady { get; set; } = false;

    // Badges are not placed anywhere, so unlike the other sections this one lists every badge.
    public Dictionary<string, InventoryBadge> BadgesByCode { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public bool IsBadgesReady { get; set; } = false;
}
