using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class BuyMarketplaceTokensMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<BuyMarketplaceTokensMessage>
{
    public async ValueTask HandleAsync(
        BuyMarketplaceTokensMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        // Inference: Flash waits for the next can-make-offer answer to carry on with the offer.
        await marketplace.BuyTokensAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        var result = await marketplace.CanMakeOfferAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceCanMakeOfferResultMessageComposer
                {
                    ResultCode = result.Code,
                    TokenCount = result.TokenCount,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
