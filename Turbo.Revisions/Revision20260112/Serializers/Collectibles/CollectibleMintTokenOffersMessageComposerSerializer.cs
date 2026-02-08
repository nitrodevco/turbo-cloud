using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class CollectibleMintTokenOffersMessageComposerSerializer(int header)
    : AbstractSerializer<CollectibleMintTokenOffersMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CollectibleMintTokenOffersMessageComposer message
    )
    {
        //
    }
}
