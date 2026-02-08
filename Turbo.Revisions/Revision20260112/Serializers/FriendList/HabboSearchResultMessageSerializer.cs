using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class HabboSearchResultMessageSerializer(int header)
    : AbstractSerializer<HabboSearchResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboSearchResultMessageComposer message
    )
    {
        packet.WriteInteger(message.Friends.Count);

        foreach (var friend in message.Friends)
        {
            MessengerSearchResultSnapshotSerializer.Serialize(packet, friend);
        }

        packet.WriteInteger(message.Others.Count);

        foreach (var other in message.Others)
        {
            MessengerSearchResultSnapshotSerializer.Serialize(packet, other);
        }
    }
}
