using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

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
        try
        {
            var snapshot = _catalogService.GetCatalogSnapshot(message.CatalogType);

            if (!snapshot.PagesById.TryGetValue(message.PageId, out var page))
                return;

            List<CatalogOfferSnapshot> offers = [];
            Dictionary<int, List<CatalogProductSnapshot>> offerProducts = [];

            if (snapshot.PageOfferIds.TryGetValue(page.Id, out var offerIds))
            {
                offers = [.. offerIds.Select(x => snapshot.OffersById[x])];
                offerProducts = offers
                    .SelectMany(x =>
                        snapshot.OfferProductIds[x.Id].Select(x => snapshot.ProductsById[x])
                    )
                    .GroupBy(x => x.OfferId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            await ctx.SendComposerAsync(
                    new CatalogPageMessageComposer
                    {
                        CatalogType = snapshot.CatalogType,
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
        catch (Exception) { }
    }
}
