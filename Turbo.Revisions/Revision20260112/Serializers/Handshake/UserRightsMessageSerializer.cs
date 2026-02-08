using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class UserRightsMessageSerializer(int header)
    : AbstractSerializer<UserRightsMessage>(header)
{
    protected override void Serialize(IServerPacket packet, UserRightsMessage message)
    {
        packet.WriteInteger((int)message.ClubLevel);
        packet.WriteInteger((int)message.SecurityLevel);
        packet.WriteBoolean(message.IsAmbassador);
    }
}
