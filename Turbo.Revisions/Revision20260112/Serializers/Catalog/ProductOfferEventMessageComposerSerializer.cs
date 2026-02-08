using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class ProductOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<ProductOfferEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ProductOfferEventMessageComposer message
    )
    {
        CatalogOfferSerializer.Serialize(packet, message.Offer);
    }
}
