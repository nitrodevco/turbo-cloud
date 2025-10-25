using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;

namespace Turbo.PacketHandlers.Tracking;

public class PerformanceLogMessageHandler : IMessageHandler<PerformanceLogMessage>
{
    public async ValueTask HandleAsync(
        PerformanceLogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
