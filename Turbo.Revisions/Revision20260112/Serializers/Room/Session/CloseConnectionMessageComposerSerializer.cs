using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class CloseConnectionMessageComposerSerializer(int header)
    : AbstractSerializer<CloseConnectionMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CloseConnectionMessageComposer message)
    {
        //
    }
}
