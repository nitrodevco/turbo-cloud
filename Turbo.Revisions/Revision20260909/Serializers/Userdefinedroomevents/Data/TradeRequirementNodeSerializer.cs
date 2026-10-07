using System;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Revisions.Revision20260909.Serializers.Vault.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>One trade rule entry; a furni entry is followed by its furni type.</summary>
internal static class TradeRequirementNodeSerializer
{
    public static void Serialize(IServerPacket packet, TradeRequirementNodeSnapshot item)
    {
        packet.WriteByte((byte)item.Type).WriteInteger(item.Amount);

        if (item.Type != TradeRequirementNodeType.Furni)
            return;

        ArgumentNullException.ThrowIfNull(item.ItemType);

        ChestItemTypeSerializer.Serialize(packet, item.ItemType);
    }
}
