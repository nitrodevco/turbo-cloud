using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

public class GetCollectibleMintingEnabledMessageHandler
    : IMessageHandler<GetCollectibleMintingEnabledMessage>
{
    public async ValueTask HandleAsync(
        GetCollectibleMintingEnabledMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
