using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class ClearMarketplaceOwnHistoryMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<ClearMarketplaceOwnHistoryMessage>
{
    public async ValueTask HandleAsync(
        ClearMarketplaceOwnHistoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var success = await marketplace
            .ClearOwnHistoryAsync(ctx.PlayerId, message.Category, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceClearOwnHistoryResultMessageComposer { Success = success },
                ct
            )
            .ConfigureAwait(false);
    }
}
