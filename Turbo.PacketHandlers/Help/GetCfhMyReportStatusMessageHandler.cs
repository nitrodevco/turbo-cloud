using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;

namespace Turbo.PacketHandlers.Help;

public class GetCfhMyReportStatusMessageHandler : IMessageHandler<GetCfhMyReportStatusMessage>
{
    public async ValueTask HandleAsync(
        GetCfhMyReportStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Calls for help are not stored yet, so the player has made none the server knows of.
        await ctx.SendComposerAsync(new MyCfhReportStatusMessageComposer { Reports = [] }, ct)
            .ConfigureAwait(false);
    }
}
