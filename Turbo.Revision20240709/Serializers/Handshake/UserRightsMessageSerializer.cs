using Turbo.Packets.Abstractions;
using Turbo.Packets.Outgoing.Handshake;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class UserRightsMessageSerializer(int header) : AbstractSerializer<UserRightsMessage>(header)
{
    protected override void Serialize(IServerPacket packet, UserRightsMessage message)
    {
        packet.WriteInteger((int)message.ClubLevel);
        packet.WriteInteger((int)message.SecurityLevel);
        packet.WriteBoolean(message.IsAmbassador);
    }
}
