using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Groupforums;

internal class UpdateMessageMessageComposerSerializer(int header)
    : AbstractSerializer<UpdateMessageMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UpdateMessageMessageComposer message)
    {
        //
    }
}
