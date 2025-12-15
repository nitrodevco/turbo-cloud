using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

            var offers = new List<CatalogOfferSnapshot>();
            var offerProducts = new Dictionary<int, ImmutableArray<CatalogProductSnapshot>>();

            foreach (var offerId in page.OfferIds)
            {
                if (snapshot.OffersById.TryGetValue(offerId, out var offer))
                    offers.Add(offer);
            }

            foreach (var offer in offers)
            {
                if (!snapshot.OfferProductIds.TryGetValue(offer.Id, out var productIds))
                    continue;

                var products = new List<CatalogProductSnapshot>();

                foreach (var productId in productIds)
                {
                    if (snapshot.ProductsById.TryGetValue(productId, out var product))
                        products.Add(product);
                }

                offerProducts[offer.Id] = [.. products];
            }

            await ctx.SendComposerAsync(
                    new CatalogPageMessageComposer
                    {
                        CatalogType = snapshot.CatalogType,
                        Page = page,
                        Offers = [.. offers],
                        OfferProducts = offerProducts.ToImmutableDictionary(),
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
