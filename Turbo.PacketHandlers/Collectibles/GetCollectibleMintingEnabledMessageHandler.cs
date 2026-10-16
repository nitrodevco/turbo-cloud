using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// Whether the relics tab may mint. Habbo answers open (official client capture, build WIN63-202610151745, an account with no wallets).
/// The tab's collect button still stays off without an active wallet (Flash <c>MintInventoryListTab.initMintItemPreview</c>), and Turbo keeps no wallets, so nothing can be minted.
/// </summary>
public class GetCollectibleMintingEnabledMessageHandler
    : IMessageHandler<GetCollectibleMintingEnabledMessage>
{
    public async ValueTask HandleAsync(
        GetCollectibleMintingEnabledMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new CollectibleMintingEnabledMessageComposer { Enabled = true },
                ct
            )
            .ConfigureAwait(false);
    }
}
