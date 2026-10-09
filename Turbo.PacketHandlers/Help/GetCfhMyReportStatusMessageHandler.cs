using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation;

namespace Turbo.PacketHandlers.Help;

public class GetCfhMyReportStatusMessageHandler(ICallForHelpService callForHelp)
    : IMessageHandler<GetCfhMyReportStatusMessage>
{
    private readonly ICallForHelpService _callForHelp = callForHelp;

    public async ValueTask HandleAsync(
        GetCfhMyReportStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendMyCfhReportStatusAsync(_callForHelp, ct).ConfigureAwait(false);
    }
}
