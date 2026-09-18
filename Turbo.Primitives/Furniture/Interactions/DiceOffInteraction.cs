using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Close a landed dice.</summary>
[GenerateSerializer, Immutable]
public sealed record DiceOffInteraction : FurnitureInteraction;
