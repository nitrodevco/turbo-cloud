using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class FriendListUpdateMessageSerializer(int header)
    : AbstractSerializer<FriendListUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FriendListUpdateMessageComposer message)
    {
        packet.WriteInteger(message.FriendCategories.Count);

        foreach (var category in message.FriendCategories)
        {
            FriendCategorySnapshotSerializer.Serialize(packet, category);
        }

        packet.WriteInteger(message.Updates.Count);

        foreach (var update in message.Updates)
        {
            packet.WriteInteger((int)update.ActionType);

            if (update.ActionType is FriendListUpdateActionType.Removed)
            {
                packet.WriteInteger(update.FriendId);

                continue;
            }

            MessengerFriendSnapshotSerializer.Serialize(packet, update.Friend);
        }
    }
}
