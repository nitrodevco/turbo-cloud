using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Snapshots.Catalog.Extensions;

namespace Turbo.PacketHandlers.Catalog;

public class GetProductOfferMessageHandler(
    ICatalogService catalogService,
    IFurnitureDefinitionProvider furnitureProvider
) : IMessageHandler<GetProductOfferMessage>
{
    private readonly ICatalogService _catalogService = catalogService;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;

    public async ValueTask HandleAsync(
        GetProductOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var catalog = _catalogService.GetCatalog(CatalogTypeEnum.Normal);

        if (catalog is null)
            return;

        var offer = catalog.GetOfferById(message.OfferId);
        var offerProducts = catalog
            .GetProductIdsByOfferId(offer.Id)
            .Select(catalog.GetProductById)
            .ToList();

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
}
