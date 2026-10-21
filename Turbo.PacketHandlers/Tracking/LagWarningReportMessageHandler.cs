using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Tracking;

/// <summary>The chat lag the client noticed, kept for the admin panel's Client health page.</summary>
public class LagWarningReportMessageHandler(IClientTelemetry telemetry)
    : IMessageHandler<LagWarningReportMessage>
{
    private readonly IClientTelemetry _telemetry = telemetry;

    public ValueTask HandleAsync(
        LagWarningReportMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        _telemetry.RecordLagWarnings(ctx.PlayerId, ctx.RoomId, message);

        return ValueTask.CompletedTask;
    }
}
