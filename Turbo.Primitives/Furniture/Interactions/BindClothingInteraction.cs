using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Bind a clothing furni's figure sets to its owner, using the furni up.</summary>
[GenerateSerializer, Immutable]
public sealed record BindClothingInteraction : FurnitureInteraction;
