using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Messages.Outgoing.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

/// <summary>A page of a game's Weekly leaderboard, as the game centre's leaderboard window scrolls it.</summary>
public class Game2GetWeeklyLeaderboardMessageHandler(IGameLeaderboardService leaderboards)
    : IMessageHandler<Game2GetWeeklyLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetWeeklyLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var board = await leaderboards
            .GetLeaderboardAsync(
                ctx.PlayerId,
                GameLeaderboardRequests.ToQuery(message, GameLeaderboardKind.Weekly),
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2WeeklyLeaderboardMessageComposer { Leaderboard = board },
                ct
            )
            .ConfigureAwait(false);
    }
}
