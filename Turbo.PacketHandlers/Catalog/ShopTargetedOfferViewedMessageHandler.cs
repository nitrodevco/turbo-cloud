using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class ShopTargetedOfferViewedMessageHandler : IMessageHandler<ShopTargetedOfferViewedMessage>
{
    public async ValueTask HandleAsync(
        ShopTargetedOfferViewedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
