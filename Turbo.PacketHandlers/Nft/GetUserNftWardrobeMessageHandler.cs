using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Messages.Outgoing.Nft;

namespace Turbo.PacketHandlers.Nft;

/// <summary>
/// The avatar editor's NFT tab asks for the player's NFT outfits (Flash
/// <c>NftAvatarsModel.requestNftAvatars</c>). The hotel has none, so the list is empty.
/// </summary>
public class GetUserNftWardrobeMessageHandler : IMessageHandler<GetUserNftWardrobeMessage>
{
    public async ValueTask HandleAsync(
        GetUserNftWardrobeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(new UserNftWardrobeMessageComposer { Items = [] }, ct)
            .ConfigureAwait(false);
    }
}
