using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.Revisions.Revision20260701.Serializers.FriendList.Snapshots;

internal class MessengerFriendSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, MessengerFriendDto message)
    {
        packet
            .WriteInteger(message.PlayerId)
            .WriteString(message.Name)
            .WriteInteger((int)message.Gender)
            .WriteBoolean(message.Online)
            .WriteBoolean(message.FollowingAllowed)
            .WriteString(message.Figure)
            .WriteInteger(message.CategoryId)
            .WriteString(message.Motto)
            .WriteString(message.RealName)
            .WriteString(message.FacebookId)
            .WriteBoolean(message.PersistedMessageUser)
            .WriteBoolean(message.VipMember)
            .WriteBoolean(message.PocketHabboUser)
            .WriteShort((short)message.RelationshipStatus);
    }
}
