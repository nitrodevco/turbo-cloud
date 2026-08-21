using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Collectibles;

internal class NftCollectionsScoreMessageComposerSerializer(int header)
    : AbstractSerializer<NftCollectionsScoreMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NftCollectionsScoreMessageComposer message
    )
    {
        //
    }
}
