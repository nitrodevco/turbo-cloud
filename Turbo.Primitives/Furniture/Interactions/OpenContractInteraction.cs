using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Show a wired contract's editor.</summary>
[GenerateSerializer, Immutable]
public sealed record OpenContractInteraction : FurnitureInteraction;
