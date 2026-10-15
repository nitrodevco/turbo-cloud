using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class TargetedOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<TargetedOfferEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TargetedOfferEventMessageComposer message
    )
    {
        // TargetedOfferData.parse (WIN63-202610131318).
        var offer = message.Offer;

        packet
            .WriteInteger(offer.TrackingState)
            .WriteInteger(offer.Id)
            .WriteString(offer.Identifier)
            .WriteString(offer.ProductCode)
            .WriteInteger(offer.PriceInCredits)
            .WriteInteger(offer.PriceInActivityPoints)
            .WriteInteger(offer.ActivityPointType)
            .WriteInteger(offer.PurchaseLimit)
            .WriteInteger(offer.SecondsToExpiry)
            .WriteString(offer.Title)
            .WriteString(offer.Description)
            .WriteString(offer.ImageUrl)
            .WriteString(offer.IconImageUrl)
            .WriteInteger(offer.Type)
            .WriteInteger(offer.SubProductCodes.Length);

        foreach (var code in offer.SubProductCodes)
            packet.WriteString(code);
    }
}
