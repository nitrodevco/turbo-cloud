using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// The collectibles hub (me menu, <c>collectibles.hub.enabled</c>) waits on this answer to leave its loading state. Turbo keeps no wallets or NFTs (there is no web3 side), so the list is always empty.
/// </summary>
public class GetNftCollectionsMessageHandler : IMessageHandler<GetNftCollectionsMessage>
{
    public async ValueTask HandleAsync(
        GetNftCollectionsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(new NftCollectionsMessageComposer(), ct).ConfigureAwait(false);
    }
}
