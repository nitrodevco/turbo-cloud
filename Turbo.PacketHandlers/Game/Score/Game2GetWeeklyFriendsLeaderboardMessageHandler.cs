using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class Game2GetWeeklyFriendsLeaderboardMessageHandler
    : IMessageHandler<Game2GetWeeklyFriendsLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetWeeklyFriendsLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
