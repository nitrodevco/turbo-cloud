using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Groupforums;

internal class UpdateThreadMessageComposerSerializer(int header)
    : AbstractSerializer<UpdateThreadMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UpdateThreadMessageComposer message)
    {
        //
    }
}
