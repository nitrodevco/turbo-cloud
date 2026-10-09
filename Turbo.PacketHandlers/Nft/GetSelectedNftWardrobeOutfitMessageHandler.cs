using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Messages.Outgoing.Nft;

namespace Turbo.PacketHandlers.Nft;

/// <summary>
/// The avatar editor asks which NFT outfit is worn when it opens and after a save (Flash
/// <c>HabboAvatarEditor.sendGetSelectedNftWardrobeOutfitMessage</c>). The hotel has no NFT
/// outfits, so none is worn and there is no fallback look to load.
/// </summary>
public class GetSelectedNftWardrobeOutfitMessageHandler
    : IMessageHandler<GetSelectedNftWardrobeOutfitMessage>
{
    public async ValueTask HandleAsync(
        GetSelectedNftWardrobeOutfitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new UserNftWardrobeSelectionMessageComposer
                {
                    CurrentTokenId = string.Empty,
                    FallbackFigure = string.Empty,
                    FallbackGender = string.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
