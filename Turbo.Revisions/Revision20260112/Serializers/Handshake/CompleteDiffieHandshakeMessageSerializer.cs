using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class CompleteDiffieHandshakeMessageSerializer(int header)
    : AbstractSerializer<CompleteDiffieHandshakeMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CompleteDiffieHandshakeMessageComposer message
    )
    {
        packet.WriteString(message.PublicKey);
        packet.WriteBoolean(message.ServerClientEncryption);
    }
}
