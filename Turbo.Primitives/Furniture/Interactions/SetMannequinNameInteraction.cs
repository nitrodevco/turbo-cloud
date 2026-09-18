using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Name the mannequin outfit.</summary>
[GenerateSerializer, Immutable]
public sealed record SetMannequinNameInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string Name { get; init; }
}
