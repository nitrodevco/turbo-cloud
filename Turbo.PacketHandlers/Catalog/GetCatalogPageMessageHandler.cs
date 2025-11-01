using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Catalog.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Snapshots.Catalog.Extensions;

namespace Turbo.PacketHandlers.Catalog;

public class GetCatalogPageMessageHandler(ICatalogService catalogService)
    : IMessageHandler<GetCatalogPageMessage>
{
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetCatalogPageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var catalog = _catalogService.GetCatalog(message.CatalogType);

        if (catalog is null)
            return;

        var page = catalog.GetPageById(message.PageId);
        var offers = catalog
            .GetOfferIdsByPageId(message.PageId)
            .Select(catalog.GetOfferById)
            .ToList();
        var offerProducts = offers
            .SelectMany(x => catalog.GetProductIdsByOfferId(x.Id).Select(catalog.GetProductById))
            .GroupBy(p => p.OfferId)
            .ToDictionary(g => g.Key, g => g.ToList());

        await ctx
            .Session.SendComposerAsync(
                new CatalogPageMessageComposer
                {
                    CatalogType = catalog.CatalogType,
                    Page = page,
                    Offers = offers,
                    OfferProducts = offerProducts,
                    OfferId = message.OfferId,
                    AcceptSeasonCurrencyAsCredits = false,
                    FrontPageItems = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
