using System;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges.Data;

/// <summary>
/// One badge as every badge list writes it: id (or slot), code, how many own it, rarity. The
/// inventory, a new badge, badge info and the badges a player wears all carry this row.
/// </summary>
internal static class BadgeEntrySerializer
{
    /// <summary>
    /// The most owners a badge row says. Habbo writes the count only up to here (official client
    /// capture: every badge row of 1000 or more owners reads exactly 1000, 498 rows; counts below
    /// it as they are, e.g. 515, 28), and the client shows the count only below it.
    /// </summary>
    public const int MAX_OWNER_COUNT = 1000;

    public static int CapOwnerCount(int ownerCount) => Math.Min(ownerCount, MAX_OWNER_COUNT);

    public static void Serialize(
        IServerPacket packet,
        int id,
        string badgeCode,
        int ownerCount,
        BadgeRarityType rarity
    ) =>
        packet
            .WriteInteger(id)
            .WriteString(badgeCode)
            .WriteInteger(CapOwnerCount(ownerCount))
            .WriteInteger((int)rarity);
}
