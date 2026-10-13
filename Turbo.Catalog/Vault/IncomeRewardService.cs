using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Vault;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Catalog.Vault;

/// <summary>
/// The earnings vault (<see cref="IIncomeRewardService"/>): the rows of every registered source,
/// and a claim paid out by the sources of its category.
/// </summary>
public sealed class IncomeRewardService(IEnumerable<IIncomeRewardSource> sources)
    : IIncomeRewardService
{
    private readonly ImmutableArray<IIncomeRewardSource> _sources = [.. sources];

    public async Task<ImmutableArray<IncomeRewardSnapshot>> GetRewardsAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var rewards = ImmutableArray.CreateBuilder<IncomeRewardSnapshot>();

        foreach (var source in _sources)
            rewards.AddRange(await source.GetRewardsAsync(player, ct).ConfigureAwait(false));

        return rewards.ToImmutable();
    }

    public async Task<bool> ClaimAsync(
        PlayerId player,
        VaultRewardCategoryType category,
        CancellationToken ct
    )
    {
        var claiming = _sources
            .Where(x => category == VaultRewardCategoryType.All || x.Category == category)
            .ToList();

        // Claim All with nothing registered has nothing to pay; one category nobody pays into fails,
        // which Flash's EarningsView.onIncomeRewardClaimResponse answers by enabling its button again.
        if (claiming.Count == 0)
            return category == VaultRewardCategoryType.All;

        var claimed = true;

        foreach (var source in claiming)
            claimed &= await source.ClaimAsync(player, ct).ConfigureAwait(false);

        return claimed;
    }
}
