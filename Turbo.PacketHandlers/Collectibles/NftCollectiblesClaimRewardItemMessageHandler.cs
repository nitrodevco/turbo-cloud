using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

public class NftCollectiblesClaimRewardItemMessageHandler
    : IMessageHandler<NftCollectiblesClaimRewardItemMessage>
{
    public async ValueTask HandleAsync(
        NftCollectiblesClaimRewardItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
