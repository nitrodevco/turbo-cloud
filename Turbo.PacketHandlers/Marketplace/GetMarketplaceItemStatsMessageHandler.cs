using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class GetMarketplaceItemStatsMessageHandler : IMessageHandler<GetMarketplaceItemStatsMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceItemStatsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
