using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;

namespace Turbo.PacketHandlers.Game.Directory;

public class Game2QuickJoinGameMessageHandler : IMessageHandler<Game2QuickJoinGameMessage>
{
    public async ValueTask HandleAsync(
        Game2QuickJoinGameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
