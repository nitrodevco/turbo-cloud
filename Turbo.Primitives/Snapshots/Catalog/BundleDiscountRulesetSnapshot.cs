namespace Turbo.Primitives.Snapshots.Catalog;

public record BundleDiscountRulesetSnapshot(
    int MaxPurchaseSize,
    int BundleSize,
    int BundleDiscountSize,
    int BonusThreshold,
    int[] AdditionalBonusDiscountThresholdQuantities
);
