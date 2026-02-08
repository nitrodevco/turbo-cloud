using Turbo.Primitives.Packets;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

internal class MessengerSearchResultSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, MessengerSearchResultSnapshot message)
    {
        packet.WriteInteger(message.PlayerId);
        packet.WriteString(message.Name);
        packet.WriteString(message.Motto);
        packet.WriteBoolean(message.Online);
        packet.WriteBoolean(message.FollowingAllowed);
        packet.WriteString(message.UnknownString);
        packet.WriteInteger((int)message.Gender);
        packet.WriteString(message.Figure);
        packet.WriteString(message.RealName);
    }
}
