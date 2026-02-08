using Turbo.Primitives.Packets;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

internal class MessengerFriendSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, MessengerFriendSnapshot message)
    {
        packet.WriteInteger(message.PlayerId);
        packet.WriteString(message.Name);
        packet.WriteInteger((int)message.Gender);
        packet.WriteBoolean(message.Online);
        packet.WriteBoolean(message.FollowingAllowed);
        packet.WriteString(message.Figure);
        packet.WriteInteger(message.CategoryId);
        packet.WriteString(message.Motto);
        packet.WriteString(message.RealName);
        packet.WriteString(message.FacebookId);
        packet.WriteBoolean(message.PersistedMessageUser);
        packet.WriteBoolean(message.VipMember);
        packet.WriteBoolean(message.PocketHabboUser);
        packet.WriteShort(message.RelationshipStatus);
    }
}
