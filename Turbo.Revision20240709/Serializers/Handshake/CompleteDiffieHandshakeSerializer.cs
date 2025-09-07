using Turbo.Packets.Abstractions;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class CompleteDiffieHandshakeSerializer(int header)
    : AbstractSerializer<CompleteDiffieHandshakeComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CompleteDiffieHandshakeComposer message)
    {
        packet.WriteString(message.PublicKey);
    }
}
