using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>The owner named the pet inside a package and wants it hatched.</summary>
[GenerateSerializer, Immutable]
public sealed record OpenPetPackageInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string Name { get; init; }
}
