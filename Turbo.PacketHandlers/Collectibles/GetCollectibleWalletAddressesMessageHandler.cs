using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// The hub asks for the player's wallets when it opens (Flash <c>CollectiblesView.requestWalletAddresses</c>). Turbo keeps no wallets or NFTs (there is no web3 side), so there is no Stardust wallet and no other: the hub shows its no-wallet state.
/// </summary>
public class GetCollectibleWalletAddressesMessageHandler
    : IMessageHandler<GetCollectibleWalletAddressesMessage>
{
    public async ValueTask HandleAsync(
        GetCollectibleWalletAddressesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new CollectibleWalletAddressesMessageComposer
                {
                    StardustWalletAddress = string.Empty,
                    WalletAddresses = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
