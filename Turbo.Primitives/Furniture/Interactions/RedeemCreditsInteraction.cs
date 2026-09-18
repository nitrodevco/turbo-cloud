using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Exchange credit furni for its face value.</summary>
[GenerateSerializer, Immutable]
public sealed record RedeemCreditsInteraction : FurnitureInteraction;
