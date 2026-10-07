using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>The give alternatives and the get rule, each behind a presence flag.</summary>
internal static class TradeRequirementRulesDefinitionSerializer
{
    public static void Serialize(IServerPacket packet, TradeRequirementRulesDefinitionSnapshot item)
    {
        packet.WriteBoolean(item.YouGive is not null);

        if (item.YouGive is { } youGive)
        {
            packet.WriteInteger(youGive.Length);

            foreach (var rule in youGive)
                TradeRequirementRuleSerializer.Serialize(packet, rule);
        }

        packet.WriteBoolean(item.YouGet is not null);

        if (item.YouGet is not null)
            TradeRequirementRuleSerializer.Serialize(packet, item.YouGet);
    }
}
