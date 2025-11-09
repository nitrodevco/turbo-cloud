using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class GetMarketplaceOffersMessageHandler : IMessageHandler<GetMarketplaceOffersMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
