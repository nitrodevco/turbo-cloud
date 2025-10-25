using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class MarkCatalogNewAdditionsPageOpenedMessageHandler
    : IMessageHandler<MarkCatalogNewAdditionsPageOpenedMessage>
{
    public async ValueTask HandleAsync(
        MarkCatalogNewAdditionsPageOpenedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
