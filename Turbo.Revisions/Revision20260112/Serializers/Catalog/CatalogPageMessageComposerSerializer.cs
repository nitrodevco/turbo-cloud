using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class CatalogPageMessageComposerSerializer(int header)
    : AbstractSerializer<CatalogPageMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CatalogPageMessageComposer message)
    {
        packet
            .WriteInteger(message.Page.Id)
            .WriteString(message.CatalogType.ToLegacyString())
            .WriteString(message.Page.Layout);

        CatalogPageLocalizationSerializer.Serialize(packet, message.Page);

        packet.WriteInteger(message.Offers.Length);

        foreach (var offer in message.Offers)
            CatalogOfferSerializer.Serialize(packet, offer);

        packet
            .WriteInteger(message.OfferId)
            .WriteBoolean(message.AcceptSeasonCurrencyAsCredits)
            .WriteInteger(message.FrontPageItems.Length);

        foreach (var frontPageItem in message.FrontPageItems)
            CatalogFrontPageItemSerializer.Serialize(packet, frontPageItem);
    }
}
