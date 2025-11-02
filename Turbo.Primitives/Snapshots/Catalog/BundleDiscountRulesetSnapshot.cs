namespace Turbo.Primitives.Snapshots.Catalog;

public sealed record BundleDiscountRulesetSnapshot(
    int MaxPurchaseSize,
    int BundleSize,
    int BundleDiscountSize,
    int BonusThreshold,
    int[] AdditionalBonusDiscountThresholdQuantities
);
