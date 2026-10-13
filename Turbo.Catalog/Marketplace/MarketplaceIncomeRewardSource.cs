using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Vault;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Catalog.Marketplace;

/// <summary>
/// Marketplace sales in the earnings vault (category 3, Flash <c>EarningsView</c>'s
/// marketplace_claim_button). Flash 1318 has no caller of RedeemMarketplaceOfferCredits
/// (HabboCatalog.as:1422) and tells sellers "Sold items will automatically be redeemed and credits
/// can be found in the Earnings window" (catalog.marketplace.own_info), so the vault is where the
/// revenue is collected. One credits row per sold offer (inference: the official client capture
/// lists achievement earnings one row per reward).
/// </summary>
public sealed class MarketplaceIncomeRewardSource(IMarketplaceService marketplace)
    : IIncomeRewardSource
{
    public VaultRewardCategoryType Category => VaultRewardCategoryType.Marketplace;

    public async Task<ImmutableArray<IncomeRewardSnapshot>> GetRewardsAsync(
        PlayerId player,
        CancellationToken ct
    ) =>
        [
            .. (await marketplace.GetUnredeemedRevenueAsync(player, ct).ConfigureAwait(false))
                .Where(x => x > 0)
                .Select(x => new IncomeRewardSnapshot
                {
                    RewardCategory = Category,
                    RewardType = VaultRewardType.Credits,
                    Amount = x,
                    ProductCode = "",
                }),
        ];

    public async Task<bool> ClaimAsync(PlayerId player, CancellationToken ct)
    {
        await marketplace.RedeemCreditsAsync(player, ct).ConfigureAwait(false);

        return true;
    }
}
