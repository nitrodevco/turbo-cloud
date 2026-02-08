using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Quest;

internal class QuestsMessageComposerSerializer(int header)
    : AbstractSerializer<QuestsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestsMessageComposer message)
    {
        //
    }
}
