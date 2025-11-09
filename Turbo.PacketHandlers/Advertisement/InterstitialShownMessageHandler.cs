using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Advertisement;

namespace Turbo.PacketHandlers.Advertisement;

public class InterstitialShownMessageHandler : IMessageHandler<InterstitialShownMessage>
{
    public async ValueTask HandleAsync(
        InterstitialShownMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
