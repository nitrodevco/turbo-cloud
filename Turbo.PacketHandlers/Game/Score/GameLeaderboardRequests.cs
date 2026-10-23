using Turbo.Primitives.Games;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

internal static class GameLeaderboardRequests
{
    public static GameLeaderboardQuery ToQuery(
        Game2LeaderboardRequest request,
        GameLeaderboardKind kind
    ) =>
        new()
        {
            Kind = kind,
            GameTypeId = request.GameTypeId,
            WeekOffset = request.WeekOffset,
            StartRank = request.StartRank,
            Upward = request.Direction == 1,
            WindowSize = request.WindowSize,
        };
}
