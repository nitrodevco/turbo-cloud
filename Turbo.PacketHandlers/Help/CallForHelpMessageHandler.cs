using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;
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
    ) =>
        await ctx.SubmitCallForHelpAsync(_callForHelp, message.Submission, ct)
            .ConfigureAwait(false);
}
