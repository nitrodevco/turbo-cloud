using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record BundleDiscountRulesetSnapshot
{
    [Id(0)]
    public required int MaxPurchaseSize;

    [Id(1)]
    public required int BundleSize;

    [Id(2)]
    public required int BundleDiscountSize;

    [Id(3)]
    public required int BonusThreshold;

    [Id(4)]
    public required int[] AdditionalBonusDiscountThresholdQuantities;
}
