using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260701.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260701.Serializers.Catalog;

internal class PurchaseOKMessageComposerSerializer(int header)
    : AbstractSerializer<PurchaseOKMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PurchaseOKMessageComposer message)
    {
        CatalogOfferSerializer.SerializeAsPurchased(packet, message.Offer);
    }
}
