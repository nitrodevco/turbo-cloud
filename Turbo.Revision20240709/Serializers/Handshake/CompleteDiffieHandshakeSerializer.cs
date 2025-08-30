using Turbo.Core.Packets.Messages;
using Turbo.Packets.Outgoing.Handshake;
using Turbo.Packets.Serializers;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class CompleteDiffieHandshakeSerializer(int header)
    : AbstractSerializer<CompleteDiffieHandshakeComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CompleteDiffieHandshakeComposer message)
    {
        packet.WriteString(message.PublicKey);
    }
}
