using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Ingame;

namespace Turbo.PacketHandlers.Game.Ingame;

public class Game2RequestFullStatusUpdateMessageHandler
    : IMessageHandler<Game2RequestFullStatusUpdateMessage>
{
    public async ValueTask HandleAsync(
        Game2RequestFullStatusUpdateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
