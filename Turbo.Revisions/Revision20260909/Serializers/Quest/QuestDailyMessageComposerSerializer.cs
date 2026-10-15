using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>QuestDailyMessageParser: whether there is a quest, then the quest and both counts.</summary>
internal class QuestDailyMessageComposerSerializer(int header)
    : AbstractSerializer<QuestDailyMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestDailyMessageComposer message)
    {
        packet.WriteBoolean(message.Quest is not null);

        if (message.Quest is null)
            return;

        QuestSerializer.Write(packet, message.Quest);
        packet.WriteInteger(message.EasyQuestCount);
        packet.WriteInteger(message.HardQuestCount);
    }
}
