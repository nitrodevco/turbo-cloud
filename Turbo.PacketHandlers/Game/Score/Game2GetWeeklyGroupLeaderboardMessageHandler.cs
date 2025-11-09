using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class Game2GetWeeklyGroupLeaderboardMessageHandler
    : IMessageHandler<Game2GetWeeklyGroupLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetWeeklyGroupLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
