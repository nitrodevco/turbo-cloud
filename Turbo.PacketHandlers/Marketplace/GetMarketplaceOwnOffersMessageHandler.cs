using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class GetMarketplaceOwnOffersMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<GetMarketplaceOwnOffersMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceOwnOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var own = await marketplace
            .GetOwnOffersAsync(ctx.PlayerId, message.Category, ct)
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
