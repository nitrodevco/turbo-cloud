using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Arena;

namespace Turbo.PacketHandlers.Game.Arena;

public class Game2ExitGameMessageHandler : IMessageHandler<Game2ExitGameMessage>
{
    public async ValueTask HandleAsync(
        Game2ExitGameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
