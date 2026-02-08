using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class CollectibleMintingEnabledMessageComposerSerializer(int header)
    : AbstractSerializer<CollectibleMintingEnabledMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CollectibleMintingEnabledMessageComposer message
    )
    {
        //
    }
}
