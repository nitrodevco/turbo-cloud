using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;

namespace Turbo.PacketHandlers.Tracking;

public class LatencyPingRequestMessageHandler : IMessageHandler<LatencyPingRequestMessage>
{
    public async ValueTask HandleAsync(
        LatencyPingRequestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
