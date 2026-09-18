using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Turn a mystery trophy into an engraved trophy.</summary>
[GenerateSerializer, Immutable]
public sealed record EngraveTrophyInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string Inscription { get; init; }
}
