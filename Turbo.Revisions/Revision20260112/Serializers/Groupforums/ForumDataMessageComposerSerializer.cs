using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Groupforums;

internal class ForumDataMessageComposerSerializer(int header)
    : AbstractSerializer<ForumDataMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ForumDataMessageComposer message)
    {
        //
    }
}
