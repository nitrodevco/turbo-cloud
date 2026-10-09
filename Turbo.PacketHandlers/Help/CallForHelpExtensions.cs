using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;

namespace Turbo.PacketHandlers.Help;

internal static class CallForHelpExtensions
{
    /// <summary>
    /// Stores a call for help, from wherever the client sent it, and answers it with
    /// <c>CallForHelpResult</c>.
    /// </summary>
    public static async Task SubmitCallForHelpAsync(
        this MessageContext ctx,
        ICallForHelpService callForHelp,
        CfhSubmissionSnapshot submission,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var result = await callForHelp
            .SubmitAsync(ctx.PlayerId, submission, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new CallForHelpResultMessageComposer { Result = result }, ct)
            .ConfigureAwait(false);
    }

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
