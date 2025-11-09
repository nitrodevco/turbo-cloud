using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;

namespace Turbo.PacketHandlers.Room.Chat;

public class CancelTypingMessageHandler : IMessageHandler<CancelTypingMessage>
{
    public async ValueTask HandleAsync(
        CancelTypingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
