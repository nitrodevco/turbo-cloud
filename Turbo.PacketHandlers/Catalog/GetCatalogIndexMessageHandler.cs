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
        var snapshot = _catalogService.GetCatalogSnapshot(message.CatalogType);

        // An index is the root page and everything under it, so a hotel that keeps no pages of
        // this type has nothing to answer with. The client leaves that catalog uninitialised,
        // which is what an empty tree should look like.
        if (!snapshot.PagesById.ContainsKey(snapshot.RootPageId))
            return;

        await ctx.SendComposerAsync(new CatalogIndexMessageComposer { Catalog = snapshot }, ct)
            .ConfigureAwait(false);
    }
}
