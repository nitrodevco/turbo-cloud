using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Nft;

public class GetSilverMessageHandler : IMessageHandler<GetSilverMessage>
{
    public async ValueTask HandleAsync(
        GetSilverMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(new SilverBalanceMessageComposer { SilverBalance = 0 }, ct)
            .ConfigureAwait(false);
    }
}
