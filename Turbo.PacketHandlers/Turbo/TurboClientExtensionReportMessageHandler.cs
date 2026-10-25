using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Plugins;

namespace Turbo.PacketHandlers.Turbo;

/// <summary>
/// A signed-in client's report on the plugins' client halves, kept for the extension inspector.
/// </summary>
public class TurboClientExtensionReportMessageHandler(IClientExtensionReports reports)
    : IMessageHandler<TurboClientExtensionReportMessage>
{
    public ValueTask HandleAsync(
        TurboClientExtensionReportMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        reports.Report(ctx.PlayerId, message.Reports);

        return ValueTask.CompletedTask;
    }
}
