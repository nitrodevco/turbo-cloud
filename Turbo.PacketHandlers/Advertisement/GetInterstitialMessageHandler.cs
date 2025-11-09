using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Advertisement;

namespace Turbo.PacketHandlers.Advertisement;

public class GetInterstitialMessageHandler : IMessageHandler<GetInterstitialMessage>
{
    public async ValueTask HandleAsync(
        GetInterstitialMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
