using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class PurchaseFromCatalogMessageHandler : IMessageHandler<PurchaseFromCatalogMessage>
{
    public async ValueTask HandleAsync(
        PurchaseFromCatalogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
