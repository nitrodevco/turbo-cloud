using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>The wired chest's window was closed.</summary>
[GenerateSerializer, Immutable]
public sealed record CloseChestInteraction : FurnitureInteraction;
