using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Nft;

internal class UserNftWardrobeSelectionMessageComposerSerializer(int header)
    : AbstractSerializer<UserNftWardrobeSelectionMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserNftWardrobeSelectionMessageComposer message
    )
    {
        //
    }
}
