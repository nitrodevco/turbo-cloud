using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Marketplace.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

internal class MarketPlaceOwnOffersEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketPlaceOwnOffersEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketPlaceOwnOffersEventMessageComposer message
    )
    {
        packet.WriteInteger(message.CreditsWaiting).WriteInteger(message.Offers.Length);

        foreach (var offer in message.Offers)
        {
            MarketplaceOfferSerializer.SerializeHead(packet, offer);

            // Only a sold or expired offer carries the time its status changed.
            if (offer.Status is MarketplaceOfferStatus.Sold or MarketplaceOfferStatus.Expired)
                packet.WriteLong(offer.StatusTime);
        }
    }
}
