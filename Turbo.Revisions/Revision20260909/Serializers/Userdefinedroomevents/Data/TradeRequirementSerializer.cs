using System;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>What a wired trade asks; the rule set follows only for the rules type.</summary>
internal static class TradeRequirementSerializer
{
    public static void Serialize(IServerPacket packet, TradeRequirementSnapshot item)
    {
        packet
            .WriteInteger((int)item.Type)
            .WriteString(item.YouGetText)
            .WriteString(item.LayoutType);

        if (item.Type != TradeRequirementType.Rules)
            return;

        ArgumentNullException.ThrowIfNull(item.Rules);

        TradeRequirementRulesSerializer.Serialize(packet, item.Rules);
    }
}
