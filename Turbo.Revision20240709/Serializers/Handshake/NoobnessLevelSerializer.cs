using Turbo.Packets.Abstractions;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class NoobnessLevelSerializer(int header) : AbstractSerializer<NoobnessLevelMessage>(header)
{
    protected override void Serialize(IServerPacket packet, NoobnessLevelMessage message)
    {
        packet.WriteInteger(0);
    }
}
