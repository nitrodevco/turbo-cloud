using System.Collections.Immutable;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Revisions.Revision20260909.Serializers.Vault.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>A transaction's row, the chests it touched, and the furni counts it deposited and withdrew.</summary>
internal static class WiredTransactionDetailsSerializer
{
    public static void Serialize(IServerPacket packet, WiredTransactionDetailsSnapshot item)
    {
        WiredTransactionInfoSerializer.Serialize(packet, item.Info);

        packet.WriteInteger(item.ChestIds.Length);

        foreach (var chestId in item.ChestIds)
            packet.WriteInteger(chestId);

        WriteCounts(packet, item.Deposited);
        WriteCounts(packet, item.Withdrawn);

        packet.WriteBoolean(item.IsIncompleteData);
    }

    private static void WriteCounts(
        IServerPacket packet,
        ImmutableArray<WiredTransactionItemCountSnapshot> counts
    )
    {
        packet.WriteInteger(counts.Length);

        foreach (var count in counts)
        {
            ChestItemTypeSerializer.Serialize(packet, count.Type);
            packet.WriteInteger(count.Count);
        }
    }
}
