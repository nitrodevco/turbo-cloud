using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.PacketHandlers.Handshake;

public class UniqueIdMessageHandler : IMessageHandler<UniqueIdMessage>
{
    public async ValueTask HandleAsync(
        UniqueIdMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
