using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class FriendRequestsMessageSerializer(int header)
    : AbstractSerializer<FriendRequestsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FriendRequestsMessageComposer message)
    {
        int totalRequests = message.Requests.Count;

        packet.WriteInteger(totalRequests);
        packet.WriteInteger(totalRequests);

        foreach (var request in message.Requests)
        {
            FriendRequestSnapshotSerializer.Serialize(packet, request);
        }
    }
}
