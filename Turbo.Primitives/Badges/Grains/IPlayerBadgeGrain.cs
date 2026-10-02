using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Badges.Grains;

/// <summary>
/// The badges one player owns and which of them they wear, keyed by player id. Its own grain,
/// not a section of the inventory: rooms read a player's worn badges on every entry, and those
/// reads must never queue behind the inventory loading thousands of furni, a trade, or a
/// purchase. Reads are interleaved; they change nothing and snapshot what they read before
/// awaiting the badge directory.
/// </summary>
public interface IPlayerBadgeGrain : IGrainWithIntegerKey
{
    [AlwaysInterleave]
    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetAllBadgeSnapshotsAsync(
        CancellationToken ct
    );

    /// <summary>One owned badge with its hotel-wide figures, or null when the player does not have it.</summary>
    [AlwaysInterleave]
    public Task<PlayerBadgeSnapshot?> GetBadgeSnapshotAsync(string badgeCode, CancellationToken ct);

    /// <summary>The worn badges, by slot.</summary>
    [AlwaysInterleave]
    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetSelectedBadgesAsync(CancellationToken ct);

    [AlwaysInterleave]
    public Task<bool> HasBadgeAsync(string badgeCode, CancellationToken ct);

    /// <summary>Gives the badge. False when the code is empty or the player already has it.</summary>
    public Task<bool> GiveBadgeAsync(string badgeCode, CancellationToken ct);

    /// <summary>
    /// The level's badge replaces every lower level of the achievement, worn or not; a worn level
    /// hands its slot to the new one. A higher level already owned is kept instead.
    /// </summary>
    public Task GrantAchievementAsync(
        int achievementId,
        int level,
        string badgeCode,
        CancellationToken ct
    );

    /// <summary>
    /// Collapses each achievement family (badge base without its level number) to the highest
    /// level the player owns. Cleans up badges granted before levels replaced one another.
    /// </summary>
    public Task NormalizeAchievementBadgesAsync(
        ImmutableArray<string> badgeBases,
        CancellationToken ct
    );

    public Task<bool> RemoveBadgeAsync(string badgeCode, CancellationToken ct);

    /// <summary>
    /// Replaces what is worn. The codes are in slot order; codes the player does not own, repeats
    /// and anything past the wearable limit are dropped. Returns what is worn afterwards.
    /// </summary>
    public Task<ImmutableArray<PlayerBadgeSnapshot>> SetActivatedBadgesAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task<PlayerBadgeSummarySnapshot> GetBadgeSummaryAsync(CancellationToken ct);

    /// <summary>
    /// Sends the player their badges tab: every badge, in fragments, and which are worn. The
    /// badge grain owns the packet, since it also sends it after a badge is taken away.
    /// </summary>
    [AlwaysInterleave]
    public Task SendBadgeInventoryAsync(CancellationToken ct);

    /// <summary>
    /// Works out the player's badges rank again and tells their player grain. Runs by itself when
    /// the badges change; the presence asks for it when the player enters a room, because a rank
    /// also moves when other players get badges.
    /// </summary>
    public Task RefreshBadgesRankAsync(CancellationToken ct);
}
