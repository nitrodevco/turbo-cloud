using Turbo.Packets.Abstractions;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.Revision20240709.Serializers.Handshake;

public class UserObjectMessageSerializer(int header) : AbstractSerializer<UserObjectMessage>(header)
{
    protected override void Serialize(IServerPacket packet, UserObjectMessage message)
    {
        packet.WriteInteger((int)message.Player.PlayerId);
        packet.WriteString(message.Player.Name);
        packet.WriteString(message.Player.Figure);
        packet.WriteString(message.Player.Gender.ToString());
        packet.WriteString(message.Player.Motto);
        packet.WriteString(message.Player.Name); // real name
        packet.WriteBoolean(false); // direct mail
        packet.WriteInteger(0); // respect total
        packet.WriteInteger(0); // respect left
        packet.WriteInteger(0); // pet respect left
        packet.WriteBoolean(false); // stream publishing enabled
        packet.WriteString(message.Player.CreatedAt.ToString()); // last online
        packet.WriteBoolean(false); // can name change
        packet.WriteBoolean(false); // account safety locked
    }
}
