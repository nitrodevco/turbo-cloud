using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>End the rent of a rentable space.</summary>
[GenerateSerializer, Immutable]
public sealed record CancelSpaceRentInteraction : FurnitureInteraction;
