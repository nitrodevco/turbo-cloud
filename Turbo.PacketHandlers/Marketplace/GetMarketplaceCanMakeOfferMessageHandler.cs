using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class GetMarketplaceCanMakeOfferMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<GetMarketplaceCanMakeOfferMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceCanMakeOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
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
