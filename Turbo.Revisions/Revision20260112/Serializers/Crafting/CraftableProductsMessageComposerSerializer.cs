using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Crafting;

internal class CraftableProductsMessageComposerSerializer(int header)
    : AbstractSerializer<CraftableProductsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CraftableProductsMessageComposer message
    )
    {
        //
    }
}
