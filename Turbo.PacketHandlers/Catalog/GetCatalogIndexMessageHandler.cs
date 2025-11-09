using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetCatalogIndexMessageHandler(ICatalogService catalogService)
    : IMessageHandler<GetCatalogIndexMessage>
{
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetCatalogIndexMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var catalog = _catalogService.GetCatalog(message.CatalogType);

        if (catalog is null)
            return;

        await ctx
            .Session.SendComposerAsync(new CatalogIndexMessageComposer { Catalog = catalog }, ct)
            .ConfigureAwait(false);
    }
}
