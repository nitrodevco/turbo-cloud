using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog.Data;

internal class ClubOfferSerializer
{
    public static void Serialize(IServerPacket packet, ClubOfferSnapshot offer)
    {
        packet
            .WriteInteger(offer.OfferId)
            .WriteString(offer.ProductCode)
            // ClubOfferData reads this and throws it away.
            .WriteBoolean(false)
            .WriteInteger(offer.PriceCredits)
            .WriteInteger(offer.PriceActivityPoints)
            .WriteInteger(offer.PriceActivityPointType)
            .WriteBoolean(offer.IsVip)
            .WriteInteger(offer.Months)
            .WriteInteger(offer.ExtraDays)
            .WriteBoolean(offer.IsGiftable)
            .WriteInteger(offer.DaysLeftAfterPurchase)
            .WriteInteger(offer.Year)
            .WriteInteger(offer.Month)
            .WriteInteger(offer.Day);
    }

    public static void SerializeExtend(IServerPacket packet, ClubExtendOfferSnapshot offer)
    {
        Serialize(packet, offer);

        packet
            .WriteInteger(offer.OriginalPriceCreditsPerPeriod)
            .WriteInteger(offer.OriginalPriceActivityPointsPerPeriod)
            .WriteInteger(offer.OriginalActivityPointType)
            .WriteInteger(offer.SubscriptionDaysLeft);
    }
}
