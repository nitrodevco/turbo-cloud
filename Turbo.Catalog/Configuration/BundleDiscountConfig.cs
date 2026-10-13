using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// The bulk-buy ruleset GetBundleDiscountRuleset tells the client: how many of an offer the
/// spinner goes up to, and the free items its bonus line counts. The defaults are Habbo's
/// (recorded on hhus, 2026-10-12): up to 100, no basic bundle, and one more free item at every
/// listed quantity.
/// </summary>
public sealed class BundleDiscountConfig
{
    /// <summary>The most of one offer bought at once; a bigger order is refused.</summary>
    public int MaxPurchaseSize { get; init; } = 100;

    public int BundleSize { get; init; } = 0;

    public int BundleDiscountSize { get; init; } = 0;

    public int BonusThreshold { get; init; } = -1;

    public int[] AdditionalBonusDiscountThresholdQuantities { get; init; } =
    [
        5, 9, 9, 13, 13, 17, 17, 21, 21, 25, 25, 29, 29, 33, 33, 37, 37, 41, 41, 45, 45, 49, 49,
        53, 53, 57, 57, 61, 61, 65, 65, 69, 69, 73, 73, 77, 77, 81, 81, 85, 85, 89, 89, 93, 93,
        97, 97,
    ];

    public BundleDiscountRulesetSnapshot ToSnapshot() =>
        new()
        {
            MaxPurchaseSize = MaxPurchaseSize,
            BundleSize = BundleSize,
            BundleDiscountSize = BundleDiscountSize,
            BonusThreshold = BonusThreshold,
            AdditionalBonusDiscountThresholdQuantities = AdditionalBonusDiscountThresholdQuantities,
        };
}
