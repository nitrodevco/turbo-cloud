using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

internal class MarketplaceConfigurationEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceConfigurationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceConfigurationEventMessageComposer message
    )
    {
        packet
            .WriteBoolean(message.Enabled)
            .WriteInteger(message.Commission)
            .WriteInteger(message.TokenBatchPrice)
            .WriteInteger(message.TokenBatchSize)
            .WriteInteger(message.OfferMinPrice)
            .WriteInteger(message.OfferMaxPrice)
            .WriteInteger(message.ExpirationHours)
            .WriteInteger(message.AveragePricePeriod)
            .WriteInteger(message.SellingFeePercentage)
            .WriteInteger(message.RevenueLimit)
            .WriteInteger(message.HalfTaxLimit);
    }
}
