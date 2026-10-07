using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault.Data;

/// <summary>A furni type as the client's <c>ChestItemType.readFromMessage</c> reads it.</summary>
internal static class ChestItemTypeSerializer
{
    public static void Serialize(IServerPacket packet, ChestItemTypeSnapshot item)
    {
        packet
            .WriteBoolean(item.IsWallItem)
            .WriteInteger(item.TypeId)
            .WriteString(item.LegacyPosterId);
    }
}
