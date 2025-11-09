using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class Game2GetWeeklyLeaderboardMessageHandler
    : IMessageHandler<Game2GetWeeklyLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetWeeklyLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
