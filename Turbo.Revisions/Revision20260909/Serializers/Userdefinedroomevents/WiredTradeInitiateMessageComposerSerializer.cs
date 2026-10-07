using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTradeInitiateMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTradeInitiateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTradeInitiateMessageComposer message
    )
    {
        TradeRequirementSerializer.Serialize(packet, message.Requirement);

        packet
            .WriteBoolean(message.ShowRequirementsImmediate)
            .WriteBoolean(message.OverridePreviousTrade)
            .WriteInteger(message.TimeoutSeconds);
    }
}
