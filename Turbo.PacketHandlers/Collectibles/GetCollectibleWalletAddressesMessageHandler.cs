using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

public class GetCollectibleWalletAddressesMessageHandler
    : IMessageHandler<GetCollectibleWalletAddressesMessage>
{
    public async ValueTask HandleAsync(
        GetCollectibleWalletAddressesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
