using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Arena;

namespace Turbo.PacketHandlers.Game.Arena;

public class Game2PlayAgainMessageHandler : IMessageHandler<Game2PlayAgainMessage>
{
    public async ValueTask HandleAsync(
        Game2PlayAgainMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
