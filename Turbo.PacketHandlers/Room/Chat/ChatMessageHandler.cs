using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;

namespace Turbo.PacketHandlers.Room.Chat;

public class ChatMessageHandler : IMessageHandler<ChatMessage>
{
    public async ValueTask HandleAsync(
        ChatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
