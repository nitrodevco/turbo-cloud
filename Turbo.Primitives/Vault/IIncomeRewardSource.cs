using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Primitives.Vault;

/// <summary>
/// One earnings category of the vault (Flash's <c>EarningsView</c>): what waits there for a player
/// and how it is paid out. A feature that pays into the vault registers one; a plugin can add its own.
/// </summary>
public interface IIncomeRewardSource
{
    public VaultRewardCategoryType Category { get; }

    /// <summary>The rows waiting for the player, each of this source's category.</summary>
    public Task<ImmutableArray<IncomeRewardSnapshot>> GetRewardsAsync(
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Pays out everything waiting; false when the claim could not be made.</summary>
    public Task<bool> ClaimAsync(PlayerId player, CancellationToken ct);
}
