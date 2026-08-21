using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260701.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260701.Serializers.FriendList;

internal class FriendListFragmentMessageSerializer(int header)
    : AbstractSerializer<FriendListFragmentMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FriendListFragmentMessageComposer message
    )
    {
        packet
            .WriteInteger(message.TotalFragments)
            .WriteInteger(message.FragmentIndex)
            .WriteInteger(message.Fragment.Count);

        foreach (var friend in message.Fragment)
            MessengerFriendSnapshotSerializer.Serialize(packet, friend);
    }
}
