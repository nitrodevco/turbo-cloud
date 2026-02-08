using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Nft;

internal class UserNftWardrobeMessageComposerSerializer(int header)
    : AbstractSerializer<UserNftWardrobeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserNftWardrobeMessageComposer message)
    {
        //
    }
}
