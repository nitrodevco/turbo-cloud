using Turbo.Core.Packets.Messages;
using Turbo.Packets.Outgoing.Handshake;
using Turbo.Packets.Serializers;

namespace Turbo.DefaultRevision.Serializers.Handshake;

public class CompleteDiffieHandshakeSerializer()
    : AbstractSerializer<CompleteDiffieHandshakeComposer>(MessageComposer.CompleteDiffieHandshakeComposer)
{
    protected override void Serialize(IServerPacket packet, CompleteDiffieHandshakeComposer message)
    {
        packet.WriteString(message.PublicKey);
    }
}