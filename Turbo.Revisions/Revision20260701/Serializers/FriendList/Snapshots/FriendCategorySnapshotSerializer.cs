using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.Revisions.Revision20260701.Serializers.FriendList.Snapshots;

internal class FriendCategorySnapshotSerializer
{
    public static void Serialize(IServerPacket packet, MessengerCategoryDto message)
    {
        packet.WriteInteger(message.CategoryId).WriteString(message.Name);
    }
}
