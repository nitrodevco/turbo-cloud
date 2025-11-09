using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

public class NftCollectiblesClaimBonusItemMessageHandler
    : IMessageHandler<NftCollectiblesClaimBonusItemMessage>
{
    public async ValueTask HandleAsync(
        NftCollectiblesClaimBonusItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
