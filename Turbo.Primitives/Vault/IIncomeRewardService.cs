using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Primitives.Vault;

/// <summary>The earnings vault across every registered <see cref="IIncomeRewardSource"/>.</summary>
public interface IIncomeRewardService
{
    /// <summary>Every row waiting for the player (Flash <c>IncomeRewardStatusMessageEvent</c>).</summary>
    public Task<ImmutableArray<IncomeRewardSnapshot>> GetRewardsAsync(
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Pays out one category, or every one for <see cref="VaultRewardCategoryType.All"/>; false
    /// when no source has the category or a payout failed.
    /// </summary>
    public Task<bool> ClaimAsync(
        PlayerId player,
        VaultRewardCategoryType category,
        CancellationToken ct
    );
}
