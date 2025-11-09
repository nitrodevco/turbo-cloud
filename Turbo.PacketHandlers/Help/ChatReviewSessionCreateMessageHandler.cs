using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class ChatReviewSessionCreateMessageHandler : IMessageHandler<ChatReviewSessionCreateMessage>
{
    public async ValueTask HandleAsync(
        ChatReviewSessionCreateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
