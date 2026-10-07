using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What a player owns: their badges, their furniture by kind (how many in their inventory and how
/// many placed in rooms), and how many pets and bots they have.
/// </summary>
public sealed record PlayerInventoryResponse(
    IReadOnlyList<PlayerBadgeItem> Badges,
    IReadOnlyList<PlayerFurnitureItem> Furniture,
    int FurnitureInInventory,
    int FurnitureInRooms,
    int Pets,
    int Bots
);
