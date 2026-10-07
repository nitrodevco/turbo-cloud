using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Buy more capacity for a wired chest.</summary>
[GenerateSerializer, Immutable]
public sealed record UpgradeChestInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int Upgrades { get; init; }
}
