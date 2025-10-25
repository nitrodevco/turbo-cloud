using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;

namespace Turbo.PacketHandlers.Tracking;

public class LatencyPingReportMessageHandler : IMessageHandler<LatencyPingReportMessage>
{
    public async ValueTask HandleAsync(
        LatencyPingReportMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
