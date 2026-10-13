using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class GetMarketplaceItemStatsMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<GetMarketplaceItemStatsMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceItemStatsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var stats = await marketplace
            .GetItemStatsAsync(message.Category, message.FurniTypeId, message.ExtraData, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceItemStatsEventMessageComposer { Stats = stats },
                ct
            )
            .ConfigureAwait(false);
    }
}
