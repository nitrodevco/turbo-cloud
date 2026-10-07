using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Show a wired chest's contents and keep them up to date until it is closed.</summary>
[GenerateSerializer, Immutable]
public sealed record OpenChestInteraction : FurnitureInteraction;
