using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>A trade rule: its entries, counted.</summary>
internal static class TradeRequirementRuleSerializer
{
    public static void Serialize(IServerPacket packet, TradeRequirementRuleSnapshot item)
    {
        packet.WriteInteger(item.Nodes.Length);

        foreach (var node in item.Nodes)
            TradeRequirementNodeSerializer.Serialize(packet, node);
    }
}
