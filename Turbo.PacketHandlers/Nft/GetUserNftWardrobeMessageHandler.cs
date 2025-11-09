using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;

namespace Turbo.PacketHandlers.Nft;

public class GetUserNftWardrobeMessageHandler : IMessageHandler<GetUserNftWardrobeMessage>
{
    public async ValueTask HandleAsync(
        GetUserNftWardrobeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
