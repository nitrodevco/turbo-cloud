using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Collectibles;

internal class CollectableMintableItemTypesMessageComposerSerializer(int header)
    : AbstractSerializer<CollectableMintableItemTypesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CollectableMintableItemTypesMessageComposer message
    )
    {
        //
    }
}
