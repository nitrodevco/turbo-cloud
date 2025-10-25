using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetMarketplaceConfigurationMessageHandler
    : IMessageHandler<GetMarketplaceConfigurationMessage>
{
    public async ValueTask HandleAsync(
        GetMarketplaceConfigurationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
