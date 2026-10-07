using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>A rule set, its scaling type, and the multiplier or cap that type reads.</summary>
internal static class TradeRequirementRulesSerializer
{
    public static void Serialize(IServerPacket packet, TradeRequirementRulesSnapshot item)
    {
        TradeRequirementRulesDefinitionSerializer.Serialize(packet, item.Definition);

        packet.WriteInteger((int)item.Type);

        switch (item.Type)
        {
            case TradeRequirementRulesType.Multiplier:
                packet.WriteInteger(item.Multiplier);
                break;
            case TradeRequirementRulesType.AutoMultiplier:
                packet.WriteInteger(item.AutoMultiplierMax);
                break;
        }
    }
}
