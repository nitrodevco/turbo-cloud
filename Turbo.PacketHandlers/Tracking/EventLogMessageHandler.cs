using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Tracking;

/// <summary>An event the client logged, kept for the admin panel's Usage page.</summary>
public class EventLogMessageHandler(IClientTelemetry telemetry) : IMessageHandler<EventLogMessage>
{
    private readonly IClientTelemetry _telemetry = telemetry;

    public ValueTask HandleAsync(EventLogMessage message, MessageContext ctx, CancellationToken ct)
    {
        _telemetry.RecordEvent(ctx.PlayerId, ctx.RoomId, message);

        return ValueTask.CompletedTask;
    }
}
