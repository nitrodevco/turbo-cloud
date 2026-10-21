using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Tracking;

/// <summary>The client's latency test result, kept for the admin panel's Client health page.</summary>
public class LatencyPingReportMessageHandler(IClientTelemetry telemetry)
    : IMessageHandler<LatencyPingReportMessage>
{
    private readonly IClientTelemetry _telemetry = telemetry;

    public ValueTask HandleAsync(
        LatencyPingReportMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        _telemetry.RecordLatency(ctx.SessionKey, ctx.PlayerId, ctx.RoomId, message);

        return ValueTask.CompletedTask;
    }
}
