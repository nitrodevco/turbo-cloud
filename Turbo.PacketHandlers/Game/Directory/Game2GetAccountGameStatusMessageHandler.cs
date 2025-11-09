using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;

namespace Turbo.PacketHandlers.Game.Directory;

public class Game2GetAccountGameStatusMessageHandler
    : IMessageHandler<Game2GetAccountGameStatusMessage>
{
    public async ValueTask HandleAsync(
        Game2GetAccountGameStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
