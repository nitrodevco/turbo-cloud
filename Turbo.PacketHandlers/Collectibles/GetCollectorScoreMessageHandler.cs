using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// A wallet's collector score. Turbo keeps no wallets or NFTs (there is no web3 side), so every wallet scores nothing at level 0.
/// </summary>
public class GetCollectorScoreMessageHandler : IMessageHandler<GetCollectorScoreMessage>
{
    public async ValueTask HandleAsync(
        GetCollectorScoreMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new NftCollectionsScoreMessageComposer
                {
                    Score = 0,
                    HighestScore = 0,
                    Level = 0,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
