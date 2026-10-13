using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Moderation;

namespace Turbo.PacketHandlers.Help;

/// <summary>
/// The pending request window's discard (<c>CallForHelpManager.deletePendingCallsForHelp</c>): the
/// open calls for help are deleted, and the answer lets the client send the new one
/// (<c>HabboHelp.onPendingCallsForHelpDeleted</c>).
/// </summary>
public class DeletePendingCallsForHelpMessageHandler(ICallForHelpService callForHelp)
    : IMessageHandler<DeletePendingCallsForHelpMessage>
{
    private readonly ICallForHelpService _callForHelp = callForHelp;

    public async ValueTask HandleAsync(
        DeletePendingCallsForHelpMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _callForHelp.DeletePendingAsync(ctx.PlayerId, ct).ConfigureAwait(false);
        await ctx.SendComposerAsync(new CallForHelpPendingCallsDeletedMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
