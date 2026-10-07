using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>A page of transaction log rows and where it sits in the whole log.</summary>
internal static class WiredTransactionLogListSerializer
{
    public static void Serialize(IServerPacket packet, WiredTransactionLogListSnapshot item)
    {
        packet
            .WriteInteger((int)item.ListType)
            .WriteLong(item.ListId)
            .WriteInteger(item.TotalLogs)
            .WriteInteger(item.CurrentPage)
            .WriteInteger(item.PageSize)
            .WriteInteger(item.Logs.Length);

        foreach (var log in item.Logs)
            WiredTransactionInfoSerializer.Serialize(packet, log);
    }
}
