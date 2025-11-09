using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class GuideSessionIsTypingMessageHandler : IMessageHandler<GuideSessionIsTypingMessage>
{
    public async ValueTask HandleAsync(
        GuideSessionIsTypingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
