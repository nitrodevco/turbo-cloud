using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class FriendNotificationMessageSerializer(int header)
    : AbstractSerializer<FriendNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FriendNotificationMessageComposer message
    )
    {
        packet.WriteString(message.AvatarId);
        packet.WriteInteger((int)message.TypeCode);
        packet.WriteString(message.Message);
    }
}
