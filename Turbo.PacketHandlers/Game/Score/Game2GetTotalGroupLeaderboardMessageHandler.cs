using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class Game2GetTotalGroupLeaderboardMessageHandler
    : IMessageHandler<Game2GetTotalGroupLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetTotalGroupLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
