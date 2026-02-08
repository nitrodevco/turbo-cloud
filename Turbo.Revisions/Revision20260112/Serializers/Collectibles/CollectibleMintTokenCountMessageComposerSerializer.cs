using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class CollectibleMintTokenCountMessageComposerSerializer(int header)
    : AbstractSerializer<CollectibleMintTokenCountMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CollectibleMintTokenCountMessageComposer message
    )
    {
        //
    }
}
