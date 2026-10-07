using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>One transaction log row.</summary>
internal static class WiredTransactionInfoSerializer
{
    public static void Serialize(IServerPacket packet, WiredTransactionInfoSnapshot item)
    {
        packet
            .WriteLong(item.TransactionId)
            .WriteInteger(item.FlatId)
            .WriteInteger((int)item.Type)
            .WriteString(item.DefinitionInfo)
            .WriteInteger(item.UserId)
            .WriteString(item.UserName)
            .WriteLong(item.Timestamp)
            .WriteString(item.ReadableTimestamp)
            .WriteInteger(item.ChestCount)
            .WriteInteger(item.WithdrawFurniCount)
            .WriteInteger(item.DepositFurniCount)
            .WriteInteger(item.WithdrawCoinsCount)
            .WriteInteger(item.DepositCoinsCount);
    }
}
