using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Nft;

public class GetNftCreditsMessageHandler : IMessageHandler<GetNftCreditsMessage>
{
    public async ValueTask HandleAsync(
        GetNftCreditsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(new EmeraldBalanceMessageComposer { EmeraldBalance = 0 }, ct)
            .ConfigureAwait(false);
    }
}
