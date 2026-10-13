namespace Turbo.Primitives.Vault.Enums;

/// <summary>
/// The earnings categories, by Flash's <c>EarningsView._rewardCategories</c> index and the
/// categories its claim buttons send (catalog/vault/EarningsView.as:14, :263-:300).
/// </summary>
public enum VaultRewardCategoryType
{
    /// <summary>Claim All (EarningsView <c>ALL_CATEGORIES</c>).</summary>
    All = -1,
    Tutorial = 0,
    DailyGift = 1,
    Achievements = 2,
    Marketplace = 3,
    HabboClub = 4,
    LevelProgression = 5,
    RoomBundleSales = 6,
    BonusBag = 7,
    Donation = 8,
    Suprise = 9,
    SnowStorm = 10,
    Games = 11,
    WiredChest = 12,
    Agency = 13,
}
