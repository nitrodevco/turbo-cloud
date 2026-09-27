using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains.Badges;

/// <summary>What a player's badge grain holds: every badge they own, by code.</summary>
internal sealed class PlayerBadgeLiveState
{
    public required PlayerId PlayerId { get; init; }

    public Dictionary<string, OwnedBadge> BadgesByCode { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A badge row as the grain keeps it. Owner count and rarity are not stored with it: they are
/// hotel-wide and change as other players get the badge, so they are read from the badge
/// directory whenever the badge is shown.
/// </summary>
internal sealed record OwnedBadge(int BadgeId, string BadgeCode, int SlotId);
