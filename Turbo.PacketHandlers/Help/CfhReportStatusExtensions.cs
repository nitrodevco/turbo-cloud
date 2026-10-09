using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Moderation;

namespace Turbo.PacketHandlers.Help;

internal static class CfhReportStatusExtensions
{
    /// <summary>Sends the player their own calls for help, as the report status window lists them.</summary>
    public static async Task SendMyCfhReportStatusAsync(
        this MessageContext ctx,
        ICallForHelpService callForHelp,
        CancellationToken ct
    ) =>
        await ctx.SendComposerAsync(
                new MyCfhReportStatusMessageComposer
                {
                    Reports = await callForHelp
                        .GetReportsAsync(ctx.PlayerId, ct)
                        .ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
}
