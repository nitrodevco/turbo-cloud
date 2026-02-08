using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class NftCollectionsMessageComposerSerializer(int header)
    : AbstractSerializer<NftCollectionsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NftCollectionsMessageComposer message)
    {
        //
    }
}
