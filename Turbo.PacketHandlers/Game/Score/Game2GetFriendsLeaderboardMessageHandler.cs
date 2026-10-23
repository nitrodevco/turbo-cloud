using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Messages.Outgoing.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

/// <summary>A page of a game's Friends leaderboard, as the game centre's leaderboard window scrolls it.</summary>
public class Game2GetFriendsLeaderboardMessageHandler(IGameLeaderboardService leaderboards)
    : IMessageHandler<Game2GetFriendsLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        Game2GetFriendsLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var board = await leaderboards
            .GetLeaderboardAsync(
                ctx.PlayerId,
                GameLeaderboardRequests.ToQuery(message, GameLeaderboardKind.Friends),
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2FriendsLeaderboardMessageComposer { Leaderboard = board },
                ct
            )
            .ConfigureAwait(false);
    }
}
