using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Ingame;

namespace Turbo.PacketHandlers.Game.Ingame;

public class Game2MakeSnowballMessageHandler : IMessageHandler<Game2MakeSnowballMessage>
{
    public async ValueTask HandleAsync(
        Game2MakeSnowballMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
