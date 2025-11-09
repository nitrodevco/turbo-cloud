using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.PacketHandlers.Handshake;

public class PongMessageHandler : IMessageHandler<PongMessage>
{
    public async ValueTask HandleAsync(
        PongMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
