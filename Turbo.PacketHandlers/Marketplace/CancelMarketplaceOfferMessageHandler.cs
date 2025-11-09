using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class CancelMarketplaceOfferMessageHandler : IMessageHandler<CancelMarketplaceOfferMessage>
{
    public async ValueTask HandleAsync(
        CancelMarketplaceOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
