using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class ApproveNameMessageComposerSerializer(int header)
    : AbstractSerializer<ApproveNameMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ApproveNameMessageComposer message)
    {
        //
    }
}
