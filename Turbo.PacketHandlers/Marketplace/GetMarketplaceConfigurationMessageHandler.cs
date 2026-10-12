using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

/// <summary>
/// Habbo answers with its marketplace settings; Turbo sends the hotel's
/// (<c>Turbo:Catalog:Marketplace</c>), off by default, so the client knows whether it is open.
/// </summary>
public class GetMarketplaceConfigurationMessageHandler(IOptions<CatalogConfig> catalogConfig)
    : IMessageHandler<GetMarketplaceConfigurationMessage>
{
    private readonly MarketplaceConfig _config = catalogConfig.Value.Marketplace;

    public async ValueTask HandleAsync(
        GetMarketplaceConfigurationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new MarketplaceConfigurationEventMessageComposer
                {
                    Enabled = _config.Enabled,
                    Commission = _config.Commission,
                    TokenBatchPrice = _config.TokenBatchPrice,
                    TokenBatchSize = _config.TokenBatchSize,
                    OfferMinPrice = _config.OfferMinPrice,
                    OfferMaxPrice = _config.OfferMaxPrice,
                    ExpirationHours = _config.ExpirationHours,
                    AveragePricePeriod = _config.AveragePricePeriod,
                    SellingFeePercentage = _config.SellingFeePercentage,
                    RevenueLimit = _config.RevenueLimit,
                    HalfTaxLimit = _config.HalfTaxLimit,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
