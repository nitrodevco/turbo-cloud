using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;

namespace Turbo.PacketHandlers.Nft;

public class GetNftCreditsMessageHandler : IMessageHandler<GetNftCreditsMessage>
{
    public async ValueTask HandleAsync(
        GetNftCreditsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
