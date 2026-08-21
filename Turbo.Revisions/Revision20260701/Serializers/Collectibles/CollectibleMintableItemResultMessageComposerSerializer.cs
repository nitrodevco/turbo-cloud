using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Collectibles;

internal class CollectibleMintableItemResultMessageComposerSerializer(int header)
    : AbstractSerializer<CollectibleMintableItemResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CollectibleMintableItemResultMessageComposer message
    )
    {
        //
    }
}
