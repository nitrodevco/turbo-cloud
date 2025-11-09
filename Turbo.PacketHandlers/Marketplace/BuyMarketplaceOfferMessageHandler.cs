using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class BuyMarketplaceOfferMessageHandler : IMessageHandler<BuyMarketplaceOfferMessage>
{
    public async ValueTask HandleAsync(
        BuyMarketplaceOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
