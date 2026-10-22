using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Messages.Outgoing.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

/// <summary>A page of a game's TotalGroup leaderboard, as the game centre's leaderboard window scrolls it.</summary>
public class Game2GetTotalGroupLeaderboardMessageHandler(IGameLeaderboardService leaderboards)
    : IMessageHandler<Game2GetTotalGroupLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetTotalGroupLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var board = await leaderboards
            .GetLeaderboardAsync(
                ctx.PlayerId,
                GameLeaderboardRequests.ToQuery(message, GameLeaderboardKind.TotalGroup),
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2TotalGroupLeaderboardMessageComposer { Leaderboard = board },
                ct
            )
            .ConfigureAwait(false);
    }
}
