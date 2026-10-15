using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Chat;

internal class UserPurchasableChatStylesMessageComposerSerializer(int header)
    : AbstractSerializer<UserPurchasableChatStylesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserPurchasableChatStylesMessageComposer message
    )
    {
        packet.WriteInteger(message.ChatStyleIds.Count);

        foreach (var chatStyleId in message.ChatStyleIds)
            packet.WriteInteger(chatStyleId);
    }
}
