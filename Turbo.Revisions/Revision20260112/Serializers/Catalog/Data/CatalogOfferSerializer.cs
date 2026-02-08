using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

internal class CatalogOfferSerializer
{
    public static void Serialize(IServerPacket packet, CatalogOfferSnapshot offer)
    {
        packet
            .WriteInteger(offer.Id)
            .WriteString(offer.LocalizationId)
            .WriteBoolean(offer.Rentable)
            .WriteInteger(offer.CostCredits)
            .WriteInteger(offer.CostCurrency)
            .WriteInteger(offer.CurrencyType ?? -1)
            .WriteInteger(offer.CostSilver)
            .WriteBoolean(offer.CanGift)
            .WriteInteger(offer.Products.Length);

        foreach (var product in offer.Products)
            CatalogProductSerializer.Serialize(packet, product);

        packet
            .WriteInteger(offer.ClubLevel)
            .WriteBoolean(offer.CanBundle)
            .WriteBoolean(false) // unknownBoolean
            .WriteString(string.Empty); // previewImage
    }

    public static void SerializeAsPurchased(IServerPacket packet, CatalogOfferSnapshot offer)
    {
        packet
            .WriteInteger(offer.Id)
            .WriteString(offer.LocalizationId)
            .WriteBoolean(offer.Rentable)
            .WriteInteger(offer.CostCredits)
            .WriteInteger(offer.CostCurrency)
            .WriteInteger(offer.CurrencyType ?? -1)
            .WriteBoolean(offer.CanGift)
            .WriteInteger(offer.Products.Length);

        foreach (var product in offer.Products)
            CatalogProductSerializer.Serialize(packet, product);

        packet.WriteInteger(offer.ClubLevel).WriteBoolean(offer.CanBundle);
    }
}
