using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Messages.Outgoing.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

/// <summary>A page of a game's WeeklyFriends leaderboard, as the game centre's leaderboard window scrolls it.</summary>
public class Game2GetWeeklyFriendsLeaderboardMessageHandler(IGameLeaderboardService leaderboards)
    : IMessageHandler<Game2GetWeeklyFriendsLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetWeeklyFriendsLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var board = await leaderboards
            .GetLeaderboardAsync(
                ctx.PlayerId,
                GameLeaderboardRequests.ToQuery(message, GameLeaderboardKind.WeeklyFriends),
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2WeeklyFriendsLeaderboardMessageComposer { Leaderboard = board },
                ct
            )
            .ConfigureAwait(false);
    }
}
