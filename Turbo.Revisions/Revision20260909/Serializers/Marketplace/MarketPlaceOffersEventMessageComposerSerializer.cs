using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Marketplace.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

internal class MarketPlaceOffersEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketPlaceOffersEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketPlaceOffersEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Offers.Length);

        foreach (var offer in message.Offers)
        {
            MarketplaceOfferSerializer.SerializeHead(packet, offer);
            packet.WriteInteger(offer.OfferCount);
        }

        packet.WriteInteger(message.TotalItemsFound);
    }
}
