using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Moderation;

internal class CfhChatlogEventMessageComposerSerializer(int header)
    : AbstractSerializer<CfhChatlogEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CfhChatlogEventMessageComposer message)
    {
        //
    }
}
