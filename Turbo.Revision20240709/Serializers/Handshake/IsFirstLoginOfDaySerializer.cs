using Turbo.Packets.Abstractions;
using Turbo.Packets.Outgoing.Handshake;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class IsFirstLoginOfDaySerializer(int header)
    : AbstractSerializer<IsFirstLoginOfDayMessage>(header)
{
    protected override void Serialize(IServerPacket packet, IsFirstLoginOfDayMessage message)
    {
        packet.WriteBoolean(false);
    }
}
