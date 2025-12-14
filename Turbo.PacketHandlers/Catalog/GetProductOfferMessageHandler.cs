using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetProductOfferMessageHandler(ICatalogService catalogService)
    : IMessageHandler<GetProductOfferMessage>
{
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetProductOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        try
        {
            var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

            if (!snapshot.OffersById.TryGetValue(message.OfferId, out var offer))
                return;

            List<CatalogProductSnapshot> offerProducts = [];

            if (snapshot.OfferProductIds.TryGetValue(offer.Id, out var productIds))
            {
                offerProducts = [.. productIds.Select(x => snapshot.ProductsById[x])];
            }

            await ctx.SendComposerAsync(
                    new ProductOfferEventMessageComposer
                    {
                        Offer = offer,
                        OfferProducts = offerProducts,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
