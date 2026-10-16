using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// A wallet's mint tokens. Turbo keeps no wallets or NFTs (there is no web3 side), so it holds none.
/// </summary>
public class GetCollectibleMintTokensMessageHandler
    : IMessageHandler<GetCollectibleMintTokensMessage>
{
    public async ValueTask HandleAsync(
        GetCollectibleMintTokensMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(new CollectibleMintTokenCountMessageComposer { Count = 0 }, ct)
            .ConfigureAwait(false);
    }
}
