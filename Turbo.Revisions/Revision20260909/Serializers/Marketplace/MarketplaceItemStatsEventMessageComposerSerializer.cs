using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

internal class MarketplaceItemStatsEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceItemStatsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceItemStatsEventMessageComposer message
    )
    {
        var stats = message.Stats;

        packet
            .WriteInteger(stats.AveragePrice)
            .WriteInteger(stats.OfferCount)
            .WriteInteger(stats.HistoryLength)
            .WriteInteger(stats.Days.Length);

        foreach (var day in stats.Days)
            packet
                .WriteInteger(day.DayOffset)
                .WriteInteger(day.AveragePrice)
                .WriteInteger(day.SoldAmount);

        packet
            .WriteInteger(stats.FurniCategoryId)
            .WriteInteger(stats.FurniTypeId)
            .WriteInteger(stats.LowestCurrentPrice)
            .WriteInteger(stats.SuggestedPrice);
    }
}
