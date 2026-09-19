using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Badges.Grains;

/// <summary>
/// The badge leaderboards and a player's place on them. One grain for the hotel, and apart from
/// <see cref="IBadgeDirectoryGrain"/> on purpose: a board is a grouped query over every badge
/// row, and every badge shown anywhere waits on the directory.
/// </summary>
public interface IBadgeLeaderboardGrain : IGrainWithStringKey
{
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
    /// <see cref="BadgeRanks.NONE"/> with none. Answered from how many players hold each score,
    /// which is read once per cache window, so it costs no query per player.
    /// </summary>
    public Task<int> GetTotalBadgesRankAsync(int totalBadges, CancellationToken ct);
}
