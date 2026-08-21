using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Handshake;

internal class InitDiffieHandshakeMessageComposerSerializer(int header)
    : AbstractSerializer<InitDiffieHandshakeMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        InitDiffieHandshakeMessageComposer message
    )
    {
        //
    }
}
