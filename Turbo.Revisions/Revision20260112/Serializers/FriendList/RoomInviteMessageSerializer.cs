using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class RoomInviteMessageSerializer(int header)
    : AbstractSerializer<RoomInviteMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomInviteMessageComposer message)
    {
        packet.WriteInteger(message.SenderId);
        packet.WriteString(message.Message);
    }
}
