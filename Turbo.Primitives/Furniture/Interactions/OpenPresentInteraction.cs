using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Unwrap a gift.</summary>
[GenerateSerializer, Immutable]
public sealed record OpenPresentInteraction : FurnitureInteraction;
