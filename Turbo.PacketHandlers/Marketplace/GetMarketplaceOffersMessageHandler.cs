using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class GetMarketplaceOffersMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<GetMarketplaceOffersMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var page = await marketplace
            .SearchOffersAsync(
                message.MinPrice,
                message.MaxPrice,
                message.SearchQuery,
                message.SortType,
                message.CombineUniques,
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketPlaceOffersEventMessageComposer
                {
                    Offers = page.Offers,
                    TotalItemsFound = page.TotalItemsFound,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
