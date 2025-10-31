using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Messages.Registry;
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
        var catalogType = CatalogTypeEnumExtensions.FromLegacyString(message.Type);
        var catalog = _catalogService.GetCatalog(catalogType);

        if (catalog is not null)
        {
            await ctx
                .Session.SendComposerAsync(
                    new CatalogIndexMessageComposer { Catalog = catalog },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
