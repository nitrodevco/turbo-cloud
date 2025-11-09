using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class PostQuizAnswersMessageHandler : IMessageHandler<PostQuizAnswersMessage>
{
    public async ValueTask HandleAsync(
        PostQuizAnswersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
