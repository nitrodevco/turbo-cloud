using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Poll;

namespace Turbo.PacketHandlers.Poll;

public class PollRejectMessageHandler : IMessageHandler<PollRejectMessage>
{
    public async ValueTask HandleAsync(
        PollRejectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
