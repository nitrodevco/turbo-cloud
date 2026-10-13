using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class BuyMarketplaceOfferMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<BuyMarketplaceOfferMessage>
{
    public async ValueTask HandleAsync(
        BuyMarketplaceOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var result = await marketplace
            .BuyOfferAsync(ctx.PlayerId, message.OfferId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceBuyOfferResultEventMessageComposer
                {
                    Result = result.Code,
                    OfferId = result.OfferId,
                    NewPrice = result.NewPrice,
                    RequestedOfferId = result.RequestedOfferId,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
