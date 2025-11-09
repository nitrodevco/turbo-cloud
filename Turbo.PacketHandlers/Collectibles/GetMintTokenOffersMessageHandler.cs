using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

public class GetMintTokenOffersMessageHandler : IMessageHandler<GetMintTokenOffersMessage>
{
    public async ValueTask HandleAsync(
        GetMintTokenOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
