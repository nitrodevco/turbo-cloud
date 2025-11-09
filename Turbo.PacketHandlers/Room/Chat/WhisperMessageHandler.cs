using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;

namespace Turbo.PacketHandlers.Room.Chat;

public class WhisperMessageHandler : IMessageHandler<WhisperMessage>
{
    public async ValueTask HandleAsync(
        WhisperMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
