using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Snapshots;

namespace Turbo.Primitives.Badges.Grains;

/// <summary>
/// What is true of badges across the whole hotel: how many players own each code and the rarity
/// that gives it. One grain for the hotel, and every badge shown anywhere asks it, so its methods
/// answer from memory. Anything that queries per request belongs in
/// <see cref="IBadgeLeaderboardGrain"/>.
/// </summary>
public interface IBadgeDirectoryGrain : IGrainWithStringKey
{
    /// <summary>Owner count and rarity per code, in the order asked. Unknown codes count no owners.</summary>
    public Task<ImmutableArray<BadgeInfoSnapshot>> GetInfoAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    );

    /// <summary>Every code that is of this rarity right now: what a rarity leaderboard counts.</summary>
    public Task<ImmutableArray<string>> GetCodesOfRarityAsync(
        BadgeRarityType rarity,
        CancellationToken ct
    );

    /// <summary>A player just received this badge; returns the figures including them.</summary>
    public Task<BadgeInfoSnapshot> OnBadgeGrantedAsync(string badgeCode, CancellationToken ct);

    public Task OnBadgeRevokedAsync(string badgeCode, CancellationToken ct);

    /// <summary>
    /// The badge a client may claim with this request code (the landing view's "request badge"
    /// button), or null when the hotel lists none for it.
    /// </summary>
    public Task<string?> GetRequestableBadgeAsync(string requestCode, CancellationToken ct);
}
