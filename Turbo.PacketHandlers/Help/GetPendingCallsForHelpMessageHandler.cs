using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class GetPendingCallsForHelpMessageHandler : IMessageHandler<GetPendingCallsForHelpMessage>
{
    public async ValueTask HandleAsync(
        GetPendingCallsForHelpMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
