using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Ingame;

namespace Turbo.PacketHandlers.Game.Ingame;

public class Game2SetUserMoveTargetMessageHandler : IMessageHandler<Game2SetUserMoveTargetMessage>
{
    public async ValueTask HandleAsync(
        Game2SetUserMoveTargetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
