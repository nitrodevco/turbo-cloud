using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Session;

namespace Turbo.PacketHandlers.Room.Session;

public class ChangeQueueMessageHandler : IMessageHandler<ChangeQueueMessage>
{
    public async ValueTask HandleAsync(
        ChangeQueueMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
