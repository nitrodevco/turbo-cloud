using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class CancelAllMarketplaceOffersMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<CancelAllMarketplaceOffersMessage>
{
    public async ValueTask HandleAsync(
        CancelAllMarketplaceOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var offerIds = await marketplace
            .CancelAllOffersAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceCancelAllOffersResultMessageComposer
                {
                    OfferIds = offerIds,
                    Success = true,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
