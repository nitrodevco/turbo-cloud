using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class InClientLinkMessageComposerSerializer(int header)
    : AbstractSerializer<InClientLinkMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, InClientLinkMessageComposer message)
    {
        //
    }
}
