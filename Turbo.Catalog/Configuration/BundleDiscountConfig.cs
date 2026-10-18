using System.Linq;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// The bulk-buy ruleset GetBundleDiscountRuleset tells the client: how many of an offer the
/// spinner goes up to, and the free items its bonus line counts, which a purchase gives on top
/// of the quantity paid for. The defaults are Habbo's (recorded on hhus, 2026-10-12): up to 100,
/// no basic bundle, and one more free item at every listed quantity.
/// </summary>
public sealed class BundleDiscountConfig
{
    /// <summary>The most of one offer bought at once; a bigger order is refused.</summary>
    public int MaxPurchaseSize { get; init; } = 100;

    /// <summary>The items to a bundle; 0 turns bundles off, and the two settings after it with them.</summary>
    public int BundleSize { get; init; } = 0;

    /// <summary>The free items the client counts for each whole bundle.</summary>
    public int BundleDiscountSize { get; init; } = 0;

    /// <summary>
    /// From this many whole bundles, the client counts one more free item per bundle past it, and
    /// one for a bundle an item short.
    /// </summary>
    public int BonusThreshold { get; init; } = -1;

    /// <summary>
    /// The quantities at each of which the client counts one more free item; a quantity listed
    /// twice counts two.
    /// </summary>
    public int[] AdditionalBonusDiscountThresholdQuantities { get; init; } =
    [
        5,
        9,
        9,
        13,
        13,
        17,
        17,
        21,
        21,
        25,
        25,
        29,
        29,
        33,
        33,
        37,
        37,
        41,
        41,
        45,
        45,
        49,
        49,
        53,
        53,
        57,
        57,
        61,
        61,
        65,
        65,
        69,
        69,
        73,
        73,
        77,
        77,
        81,
        81,
        85,
        85,
        89,
        89,
        93,
        93,
        97,
        97,
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

    /// <summary>
    /// The free items that buying <paramref name="quantity"/> of an offer earns, counted as the
    /// client counts its bonus line (<c>HabboCatalogUtils.getDiscountItemsCount</c>): a lot per
    /// whole bundle, the bonus from <see cref="BonusThreshold"/> bundles up, and one per listed
    /// quantity reached.
    /// </summary>
    public int FreeItems(int quantity)
    {
        var free = AdditionalBonusDiscountThresholdQuantities.Count(x => quantity >= x);

        if (BundleSize <= 0)
            return free;

        var bundles = quantity / BundleSize;

        free += bundles * BundleDiscountSize;

        if (bundles >= BonusThreshold)
        {
            if (quantity % BundleSize == BundleSize - 1)
                free++;

            free += bundles - BonusThreshold;
        }

        return free;
    }
}
