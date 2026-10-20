using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Tracking;

/// <summary>The client's performance report, kept for the admin panel's Client health page.</summary>
public class PerformanceLogMessageHandler(IClientTelemetry telemetry)
    : IMessageHandler<PerformanceLogMessage>
{
    private readonly IClientTelemetry _telemetry = telemetry;

    public ValueTask HandleAsync(
        PerformanceLogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        _telemetry.RecordPerformance(ctx.SessionKey, ctx.PlayerId, ctx.RoomId, message);

        return ValueTask.CompletedTask;
    }
}
