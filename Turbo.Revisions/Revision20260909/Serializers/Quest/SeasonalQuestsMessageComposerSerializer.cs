using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

internal class SeasonalQuestsMessageComposerSerializer(int header)
    : AbstractSerializer<SeasonalQuestsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, SeasonalQuestsMessageComposer message)
    {
        packet.WriteInteger(message.Quests.Length);

        foreach (var quest in message.Quests)
            QuestSerializer.Write(packet, quest);
    }
}
