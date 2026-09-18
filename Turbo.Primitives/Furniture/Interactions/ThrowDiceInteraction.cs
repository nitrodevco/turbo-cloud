using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Double-click on a closed dice.</summary>
[GenerateSerializer, Immutable]
public sealed record ThrowDiceInteraction : FurnitureInteraction;
