using System;
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
        try
        {
            var snapshot = _catalogService.GetCatalogSnapshot(message.CatalogType);

            await ctx.SendComposerAsync(new CatalogIndexMessageComposer { Catalog = snapshot }, ct)
                .ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
