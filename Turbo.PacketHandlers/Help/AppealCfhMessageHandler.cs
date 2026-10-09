using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation;

namespace Turbo.PacketHandlers.Help;

public class AppealCfhMessageHandler(ICallForHelpService callForHelp)
    : IMessageHandler<AppealCfhMessage>
{
    private readonly ICallForHelpService _callForHelp = callForHelp;

    public async ValueTask HandleAsync(
        AppealCfhMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.ReportId <= 0)
            return;

        if (
            !await _callForHelp
                .AppealAsync(ctx.PlayerId, message.ReportId, ct)
                .ConfigureAwait(false)
        )
            return;

        // The report status window is open on the report; the new list shows it appealed.
        await ctx.SendMyCfhReportStatusAsync(_callForHelp, ct).ConfigureAwait(false);
    }
}
