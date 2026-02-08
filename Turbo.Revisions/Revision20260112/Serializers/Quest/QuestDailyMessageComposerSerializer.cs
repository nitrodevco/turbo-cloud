using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Quest;

internal class QuestDailyMessageComposerSerializer(int header)
    : AbstractSerializer<QuestDailyMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestDailyMessageComposer message)
    {
        //
    }
}
