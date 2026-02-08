using Turbo.Primitives.Packets;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

internal class FriendRequestSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, FriendRequestSnapshot message)
    {
        packet.WriteInteger(message.RequestId);
        packet.WriteString(message.RequesterName);
        packet.WriteString(message.FigureString);
        packet.WriteInteger(message.RequesterUserId);
    }
}
