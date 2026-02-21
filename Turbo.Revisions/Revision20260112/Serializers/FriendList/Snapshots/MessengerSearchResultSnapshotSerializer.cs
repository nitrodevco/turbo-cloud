using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

internal class MessengerSearchResultSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, MessengerSearchResultSnapshot message)
    {
        packet
            .WriteInteger(message.PlayerId)
            .WriteString(message.Name)
            .WriteString(message.Motto)
            .WriteBoolean(message.Online)
            .WriteBoolean(message.FollowingAllowed)
            .WriteString(message.UnknownString)
            .WriteInteger((int)message.Gender)
            .WriteString(message.Figure)
            .WriteString(message.RealName);
    }
}
