using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>QuestCompletedMessageParser: the quest, then whether to show the dialog.</summary>
internal class QuestCompletedMessageComposerSerializer(int header)
    : AbstractSerializer<QuestCompletedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestCompletedMessageComposer message)
    {
        QuestSerializer.Write(packet, message.Quest);
        packet.WriteBoolean(message.ShowDialog);
    }
}
