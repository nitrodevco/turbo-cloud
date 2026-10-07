using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Open the trade window to put things into a wired chest.</summary>
[GenerateSerializer, Immutable]
public sealed record StartChestDepositInteraction : FurnitureInteraction;
