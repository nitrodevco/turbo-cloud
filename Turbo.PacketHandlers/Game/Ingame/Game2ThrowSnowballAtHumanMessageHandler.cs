using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Ingame;

namespace Turbo.PacketHandlers.Game.Ingame;

public class Game2ThrowSnowballAtHumanMessageHandler
    : IMessageHandler<Game2ThrowSnowballAtHumanMessage>
{
    public async ValueTask HandleAsync(
        Game2ThrowSnowballAtHumanMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
