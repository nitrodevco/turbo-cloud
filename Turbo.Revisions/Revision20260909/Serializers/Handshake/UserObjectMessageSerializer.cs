using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Handshake;

internal class UserObjectMessageSerializer(int header)
    : AbstractSerializer<UserObjectMessage>(header)
{
    protected override void Serialize(IServerPacket packet, UserObjectMessage message)
    {
        packet.WriteInteger(message.Player.PlayerId);
        packet.WriteString(message.Player.Name);
        packet.WriteString(message.Player.Figure);
        packet.WriteString(message.Player.Gender.ToLegacyString());
        packet.WriteString(message.Player.Motto);
        packet.WriteString(message.Player.Name); // real name
        packet.WriteBoolean(false); // direct mail
        packet.WriteInteger(message.Player.RespectPoints);
        packet.WriteInteger(message.Player.RespectsLeft);
        packet.WriteInteger(message.Player.PetRespectsLeft);
        packet.WriteBoolean(false); // stream publishing enabled
        packet.WriteString(message.Player.CreatedAt.ToString()); // last online
        packet.WriteBoolean(false); // can name change
        packet.WriteBoolean(false); // account safety locked
        packet.WriteBoolean(false); // account trade locked
        packet.WriteString(string.Empty); // name colour
        packet.WriteInteger(message.Player.RespectReplenishesLeft);
        packet.WriteInteger(message.MaxRespectPerDay);
    }
}
