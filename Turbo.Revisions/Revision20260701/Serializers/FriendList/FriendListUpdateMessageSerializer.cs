using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Revisions.Revision20260701.Serializers.FriendList.Snapshots;

namespace Turbo.Revisions.Revision20260701.Serializers.FriendList;

internal class FriendListUpdateMessageSerializer(int header)
    : AbstractSerializer<FriendListUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FriendListUpdateMessageComposer message)
    {
        packet.WriteInteger(message.Categories.Count);

        foreach (var category in message.Categories)
            FriendCategorySnapshotSerializer.Serialize(packet, category);

        packet.WriteInteger(message.Updates.Count);

        foreach (var update in message.Updates)
        {
            packet.WriteInteger((int)update.ActionType);

            if (update.ActionType is FriendListUpdateActionType.Removed)
            {
                packet.WriteInteger(update.FriendId);

                continue;
            }

            if (update.Friend is not null)
                MessengerFriendSnapshotSerializer.Serialize(packet, update.Friend);
        }
    }
}
