using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>QuestCancelledMessageParser: expired, then the quest.</summary>
internal class QuestCancelledMessageComposerSerializer(int header)
    : AbstractSerializer<QuestCancelledMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestCancelledMessageComposer message)
    {
        packet.WriteBoolean(message.Expired);
        QuestSerializer.Write(packet, message.Quest);
    }
}
