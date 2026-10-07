using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault.Data;

/// <summary>One stored item as the client's <c>ChestStorage</c> reads it; the extra int follows only for a floor item.</summary>
internal static class ChestStorageSerializer
{
    public static void Serialize(IServerPacket packet, ChestStorageSnapshot item)
    {
        packet.WriteInteger(item.ItemId).WriteInteger(item.LockState).WriteLong(item.TransactionId);

        ChestItemTypeSerializer.Serialize(packet, item.Type);

        packet.WriteBoolean(item.Groupable).WriteInteger(item.SpecialType);

        StuffDataSnapshotSerializer.Serialize(packet, item.StuffData);

        if (!item.Type.IsWallItem)
            packet.WriteInteger(item.Extra);
    }
}
