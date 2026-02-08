using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class MiniMailUnreadCountMessageSerializer(int header)
    : AbstractSerializer<MiniMailUnreadCountMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MiniMailUnreadCountMessageComposer message
    )
    {
        packet.WriteInteger(message.UnreadCount);
    }
}
