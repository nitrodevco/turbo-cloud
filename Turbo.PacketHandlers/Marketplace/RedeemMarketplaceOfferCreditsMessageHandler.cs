using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class RedeemMarketplaceOfferCreditsMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<RedeemMarketplaceOfferCreditsMessage>
{
    public async ValueTask HandleAsync(
        RedeemMarketplaceOfferCreditsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await marketplace.RedeemCreditsAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        // The sold list again, its credits now paid out.
        var own = await marketplace
            .GetOwnOffersAsync(ctx.PlayerId, (int)MarketplaceOfferStatus.Sold, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketPlaceOwnOffersEventMessageComposer
                {
                    CreditsWaiting = own.CreditsWaiting,
                    Offers = own.Offers,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
