using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class NewFriendRequestMessageSerializer(int header)
    : AbstractSerializer<NewFriendRequestMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NewFriendRequestMessageComposer message)
    {
        FriendRequestSnapshotSerializer.Serialize(packet, message.Request);
    }
}
