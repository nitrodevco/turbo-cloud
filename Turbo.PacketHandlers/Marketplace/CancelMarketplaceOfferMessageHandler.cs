using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class CancelMarketplaceOfferMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<CancelMarketplaceOfferMessage>
{
    public async ValueTask HandleAsync(
        CancelMarketplaceOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var success = await marketplace
            .CancelOfferAsync(ctx.PlayerId, message.OfferId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceCancelOfferResultEventMessageComposer
                {
                    OfferId = message.OfferId,
                    Success = success,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
