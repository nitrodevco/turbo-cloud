using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;

namespace Turbo.PacketHandlers.Nft;

public class GetSelectedNftWardrobeOutfitMessageHandler
    : IMessageHandler<GetSelectedNftWardrobeOutfitMessage>
{
    public async ValueTask HandleAsync(
        GetSelectedNftWardrobeOutfitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
