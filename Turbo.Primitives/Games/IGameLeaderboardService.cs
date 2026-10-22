using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Games;

/// <summary>Which board a <c>Game2Get*Leaderboard</c> asks for.</summary>
public enum GameLeaderboardKind
{
    Total,
    Weekly,
    Friends,
    WeeklyFriends,
    TotalGroup,
    WeeklyGroup,
}

/// <summary>
/// A leaderboard request as the client sends it (<c>LeaderboardTable.getMessageComposer</c>):
/// the first rank wanted, -1 for the page around the player, and the direction to read from it.
/// </summary>
public sealed record GameLeaderboardQuery
{
    public required GameLeaderboardKind Kind { get; init; }

    public required int GameTypeId { get; init; }

    /// <summary>Weeks back from this one, for a weekly board.</summary>
    public int WeekOffset { get; init; }

    public required int StartRank { get; init; }

    /// <summary>True reads the ranks above <see cref="StartRank"/> (scrolling up).</summary>
    public required bool Upward { get; init; }

    public required int WindowSize { get; init; }
}

/// <summary>Reads the game leaderboards from the stored results.</summary>
public interface IGameLeaderboardService
{
    public Task<GameLeaderboardSnapshot> GetLeaderboardAsync(
        PlayerId playerId,
        GameLeaderboardQuery query,
        CancellationToken ct
    );
}
