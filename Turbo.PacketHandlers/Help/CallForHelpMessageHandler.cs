using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Moderation;

namespace Turbo.PacketHandlers.Help;

public class CallForHelpMessageHandler(ICallForHelpService callForHelp)
    : IMessageHandler<CallForHelpMessage>
{
    private readonly ICallForHelpService _callForHelp = callForHelp;

    public async ValueTask HandleAsync(
        CallForHelpMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var result = await _callForHelp
            .SubmitAsync(ctx.PlayerId, message.Submission, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new CallForHelpResultMessageComposer { Result = result }, ct)
            .ConfigureAwait(false);
    }
}
