using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Groupforums;

internal class PostMessageMessageComposerSerializer(int header)
    : AbstractSerializer<PostMessageMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PostMessageMessageComposer message)
    {
        //
    }
}
