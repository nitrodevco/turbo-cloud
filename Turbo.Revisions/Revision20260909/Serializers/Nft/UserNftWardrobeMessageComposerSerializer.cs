using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Nft;

internal class UserNftWardrobeMessageComposerSerializer(int header)
    : AbstractSerializer<UserNftWardrobeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserNftWardrobeMessageComposer message)
    {
        packet.WriteInteger(message.Items.Length);

        foreach (var item in message.Items)
            packet
                .WriteString(item.Id)
                .WriteString(item.Figure)
                .WriteString(item.Gender)
                .WriteString(item.TokenId)
                .WriteString(item.ContractKey);
    }
}
