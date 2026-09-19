using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Rent a rentable space.</summary>
[GenerateSerializer, Immutable]
public sealed record RentSpaceInteraction : FurnitureInteraction;
