using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Quest;

internal class QuestMessageComposerSerializer(int header)
    : AbstractSerializer<QuestMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestMessageComposer message)
    {
        //
    }
}
