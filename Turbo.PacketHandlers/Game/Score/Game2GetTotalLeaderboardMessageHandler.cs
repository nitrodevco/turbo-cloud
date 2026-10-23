using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Messages.Outgoing.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

/// <summary>A page of a game's Total leaderboard, as the game centre's leaderboard window scrolls it.</summary>
public class Game2GetTotalLeaderboardMessageHandler(IGameLeaderboardService leaderboards)
    : IMessageHandler<Game2GetTotalLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetTotalLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var board = await leaderboards
            .GetLeaderboardAsync(
                ctx.PlayerId,
                GameLeaderboardRequests.ToQuery(message, GameLeaderboardKind.Total),
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2TotalLeaderboardMessageComposer { Leaderboard = board },
                ct
            )
            .ConfigureAwait(false);
    }
}
