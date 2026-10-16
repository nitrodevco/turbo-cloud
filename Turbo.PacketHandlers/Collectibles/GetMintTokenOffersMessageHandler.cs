using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// The collectibles hub (me menu, <c>collectibles.hub.enabled</c>) waits on this answer to leave its loading state. Turbo keeps no wallets or NFTs (there is no web3 side), so the list is always empty.
/// </summary>
public class GetMintTokenOffersMessageHandler : IMessageHandler<GetMintTokenOffersMessage>
{
    public async ValueTask HandleAsync(
        GetMintTokenOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(new CollectibleMintTokenOffersMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
