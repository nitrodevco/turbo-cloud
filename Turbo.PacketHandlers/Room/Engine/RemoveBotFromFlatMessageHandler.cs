using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class RemoveBotFromFlatMessageHandler : IMessageHandler<RemoveBotFromFlatMessage>
{
    public async ValueTask HandleAsync(
        RemoveBotFromFlatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
