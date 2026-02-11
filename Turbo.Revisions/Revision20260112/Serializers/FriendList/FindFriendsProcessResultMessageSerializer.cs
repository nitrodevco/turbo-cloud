using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class FindFriendsProcessResultMessageSerializer(int header)
    : AbstractSerializer<FindFriendsProcessResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FindFriendsProcessResultMessageComposer message
    )
    {
        packet.WriteBoolean(message.Success);
    }
}
