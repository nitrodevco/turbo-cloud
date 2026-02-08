using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class IgnoreResultMessageComposerSerializer(int header)
    : AbstractSerializer<IgnoreResultMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, IgnoreResultMessageComposer message)
    {
        //
    }
}
