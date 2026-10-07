using System;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

/// <summary>A completed transaction's type; a reward is followed by what was given, its text and whether to open it.</summary>
internal static class WiredTransactionSuccessContentsSerializer
{
    public static void Serialize(IServerPacket packet, WiredTransactionSuccessContentsSnapshot item)
    {
        packet.WriteInteger((int)item.Type);

        if (item.Type != WiredTransactionSuccessType.Rewarded)
            return;

        ArgumentNullException.ThrowIfNull(item.RewardContents);

        TradeRequirementRuleSerializer.Serialize(packet, item.RewardContents);

        packet.WriteString(item.RewardText ?? string.Empty).WriteBoolean(item.OpenByDefault);
    }
}
