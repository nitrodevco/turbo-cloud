using Turbo.Core.Packets.Messages;
using Turbo.Packets.Outgoing.Handshake;
using Turbo.Packets.Serializers;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class InitDiffieHandshakeSerializer(int header)
    : AbstractSerializer<InitDiffieHandshakeComposer>(header)
{
    protected override void Serialize(IServerPacket packet, InitDiffieHandshakeComposer composer) =>
        packet.WriteString(composer.Prime).WriteString(composer.Generator);
}
