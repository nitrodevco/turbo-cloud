using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Moderation;

namespace Turbo.PacketHandlers.Help;

/// <summary>
/// Asked before every report (<c>HabboHelp.queryForPendingCallsForHelp</c>): the reporter's calls
/// for help still open, which the client offers to keep or discard before a new one.
/// </summary>
public class GetPendingCallsForHelpMessageHandler(ICallForHelpService callForHelp)
    : IMessageHandler<GetPendingCallsForHelpMessage>
{
    private readonly ICallForHelpService _callForHelp = callForHelp;

    public async ValueTask HandleAsync(
        GetPendingCallsForHelpMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new CallForHelpPendingCallsMessageComposer
                {
                    Calls = await _callForHelp
                        .GetPendingAsync(ctx.PlayerId, ct)
                        .ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
