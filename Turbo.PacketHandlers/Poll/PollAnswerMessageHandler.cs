using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Poll;

namespace Turbo.PacketHandlers.Poll;

public class PollAnswerMessageHandler : IMessageHandler<PollAnswerMessage>
{
    public async ValueTask HandleAsync(
        PollAnswerMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
