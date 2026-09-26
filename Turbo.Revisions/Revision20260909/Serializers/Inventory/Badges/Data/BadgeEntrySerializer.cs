using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges.Data;

/// <summary>
/// One badge as every badge list writes it: id (or slot), code, how many own it, rarity. The
/// inventory, a new badge, badge info and the badges a player wears all carry this row.
/// </summary>
internal static class BadgeEntrySerializer
{
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
            .WriteInteger(ownerCount)
            .WriteInteger((int)rarity);
}
