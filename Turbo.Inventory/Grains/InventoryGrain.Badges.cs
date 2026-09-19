using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetAllBadgeSnapshotsAsync(
        CancellationToken ct
    ) => _badgeModule.GetAllAsync(ct);

    public Task<PlayerBadgeSnapshot?> GetBadgeSnapshotAsync(
        string badgeCode,
        CancellationToken ct
    ) => _badgeModule.GetAsync(badgeCode, ct);

    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetSelectedBadgesAsync(CancellationToken ct) =>
        _badgeModule.GetSelectedAsync(ct);

    public Task<bool> HasBadgeAsync(string badgeCode, CancellationToken ct) =>
        _badgeModule.HasAsync(badgeCode, ct);

    public Task<bool> GiveBadgeAsync(string badgeCode, CancellationToken ct) =>
        _badgeModule.GiveAsync(badgeCode, ct);

    public Task<bool> RemoveBadgeAsync(string badgeCode, CancellationToken ct) =>
        _badgeModule.RemoveAsync(badgeCode, ct);

    public Task<ImmutableArray<PlayerBadgeSnapshot>> SetActivatedBadgesAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    ) => _badgeModule.SetActivatedAsync(badgeCodes, ct);

    public Task<PlayerBadgeSummarySnapshot> GetBadgeSummaryAsync(CancellationToken ct) =>
        _badgeModule.GetSummaryAsync(ct);

    public Task RefreshBadgesRankAsync(CancellationToken ct) => _badgeModule.RefreshRankAsync(ct);
}
