using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Game.Score;

/// <summary>
/// What every <c>Game2Get*Leaderboard</c> sends (<c>LeaderboardTable.getMessageComposer</c>): the
/// game, the week for a weekly board, the first rank wanted (-1 for the page around the player),
/// the direction (1 reads upward) and the client's view and window sizes.
/// </summary>
public abstract record Game2LeaderboardRequest : IMessageEvent
{
    public required int GameTypeId { get; init; }

    public int WeekOffset { get; init; }

    public required int StartRank { get; init; }

    public required int Direction { get; init; }

    public required int ViewSize { get; init; }

    public required int WindowSize { get; init; }
}
