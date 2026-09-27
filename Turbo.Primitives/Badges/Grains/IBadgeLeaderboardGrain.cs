using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Badges.Grains;

/// <summary>
/// The badge leaderboards and a player's place on them. One grain for the hotel, and apart from
/// <see cref="IBadgeDirectoryGrain"/> on purpose: a board is a grouped query over every badge
/// row, and every badge shown anywhere waits on the directory.
///
/// Both methods are interleaved. The boards are read on a timer and swapped in whole, so a
/// request never waits behind a refresh, and the few reads a request still makes (a board's
/// first read, a chunk past the entries held) hold nothing across their awaits.
/// </summary>
public interface IBadgeLeaderboardGrain : IGrainWithStringKey
{
    [AlwaysInterleave]
    public Task<BadgeLeaderboardPageSnapshot> GetLeaderboardAsync(
        BadgeLeaderboardType type,
        int rarity,
        int chunkIndex,
        int chunkSize,
        PlayerId forPlayerId,
        CancellationToken ct
    );

    /// <summary>
    /// The place a player owning this many badges has on the total badges board, or
    /// <see cref="BadgeRanks.NONE"/> with none. Answered from memory: how many players hold
    /// each score, read on the grain's refresh timer, so it costs no query per player.
    /// </summary>
    [AlwaysInterleave]
    public Task<int> GetTotalBadgesRankAsync(int totalBadges, CancellationToken ct);
}
