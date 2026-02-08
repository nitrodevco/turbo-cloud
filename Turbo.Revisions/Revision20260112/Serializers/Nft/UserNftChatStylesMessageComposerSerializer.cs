using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Nft;

internal class UserNftChatStylesMessageComposerSerializer(int header)
    : AbstractSerializer<UserNftChatStylesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserNftChatStylesMessageComposer message
    )
    {
        packet.WriteInteger(message.ChatStyleIds.Count);

        foreach (var chatStyleId in message.ChatStyleIds)
            packet.WriteInteger(chatStyleId);
    }
}
