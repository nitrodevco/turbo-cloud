using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Snapshots;

namespace Turbo.Players.Grains.Badges;

/// <summary>
/// One board as it was last read. Immutable and replaced whole, so a request can read it while
/// the refresh reads the next one.
/// </summary>
/// <param name="Codes">The codes the board counts, or null for every badge.</param>
/// <param name="Histogram">How many players hold each score, highest score first.</param>
/// <param name="TotalEntries">Players on the board.</param>
/// <param name="Top">The first ranked entries, as many as the config holds.</param>
internal sealed record BadgeLeaderboardBoard(
    BadgeLeaderboardType Type,
    ImmutableArray<string>? Codes,
    ImmutableArray<(int Score, int Players)> Histogram,
    int TotalEntries,
    ImmutableArray<BadgeLeaderboardEntrySnapshot> Top
)
{
    /// <summary>
    /// The rank of a score alone, for an avatar's badge rank: one more than the players with a
    /// higher one, so ties share it. The board's own lines are numbered by place instead.
    /// </summary>
    public int RankOf(int score) =>
        Histogram.TakeWhile(x => x.Score > score).Sum(x => x.Players) + 1;
}
