using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class MessengerInitMessageSerializer(int header)
    : AbstractSerializer<MessengerInitMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, MessengerInitMessageComposer message)
    {
        packet.WriteInteger(message.UserFriendLimit);
        packet.WriteInteger(message.NormalFriendLimit);
        packet.WriteInteger(message.ExtendedFriendLimit);
        packet.WriteInteger(message.FriendCategories.Count);

        foreach (var category in message.FriendCategories)
        {
            FriendCategorySnapshotSerializer.Serialize(packet, category);
        }
    }
}
