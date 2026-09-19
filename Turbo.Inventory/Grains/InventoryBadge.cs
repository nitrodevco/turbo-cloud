namespace Turbo.Inventory.Grains;

/// <summary>
/// A badge row as the inventory keeps it. Owner count and rarity are not stored with it: they
/// are hotel-wide and change as other players get the badge, so they are read from the badge
/// directory whenever the badge is shown.
/// </summary>
internal sealed record InventoryBadge(int BadgeId, string BadgeCode, int SlotId);
