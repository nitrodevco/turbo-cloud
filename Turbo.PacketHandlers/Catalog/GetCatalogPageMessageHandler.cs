using System.Threading;
using System.Threading.Tasks;
using Turbo.Catalog.Abstractions;
using Turbo.Furniture.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetCatalogPageMessageHandler(
    ICatalogService catalogService,
    IFurnitureProvider furnitureProvider
) : IMessageHandler<GetCatalogPageMessage>
{
    private readonly ICatalogService _catalogService = catalogService;
    private readonly IFurnitureProvider _furnitureProvider = furnitureProvider;

    public async ValueTask HandleAsync(
        GetCatalogPageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var catalog = _catalogService.GetCatalog(message.CatalogType);

        if (catalog is null)
            return;

        await ctx
            .Session.SendComposerAsync(
                new CatalogPageMessageComposer
                {
                    Catalog = catalog,
                    Furniture = _furnitureProvider.Current,
                    PageId = message.PageId,
                    OfferId = message.OfferId,
                    AcceptSeasonCurrencyAsCredits = false,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
