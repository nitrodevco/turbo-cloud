using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class CallForHelpFromForumThreadMessageHandler
    : IMessageHandler<CallForHelpFromForumThreadMessage>
{
    public async ValueTask HandleAsync(
        CallForHelpFromForumThreadMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
