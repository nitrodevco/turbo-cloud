using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetAllBadgeSnapshotsAsync(
        CancellationToken ct
    );

    /// <summary>One owned badge with its hotel-wide figures, or null when the player does not have it.</summary>
    public Task<PlayerBadgeSnapshot?> GetBadgeSnapshotAsync(string badgeCode, CancellationToken ct);

    /// <summary>The worn badges, by slot.</summary>
    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetSelectedBadgesAsync(CancellationToken ct);

    public Task<bool> HasBadgeAsync(string badgeCode, CancellationToken ct);

    /// <summary>Gives the badge. False when the code is empty or the player already has it.</summary>
    public Task<bool> GiveBadgeAsync(string badgeCode, CancellationToken ct);

    public Task<bool> RemoveBadgeAsync(string badgeCode, CancellationToken ct);

    /// <summary>
    /// Replaces what is worn. The codes are in slot order; codes the player does not own, repeats
    /// and anything past the wearable limit are dropped. Returns what is worn afterwards.
    /// </summary>
    public Task<ImmutableArray<PlayerBadgeSnapshot>> SetActivatedBadgesAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    );

    public Task<PlayerBadgeSummarySnapshot> GetBadgeSummaryAsync(CancellationToken ct);

    /// <summary>
    /// Works out the player's badges rank again and tells their player grain. Runs by itself when
    /// the badges change; the presence asks for it when the player enters a room, because a rank
    /// also moves when other players get badges.
    /// </summary>
    public Task RefreshBadgesRankAsync(CancellationToken ct);
}
