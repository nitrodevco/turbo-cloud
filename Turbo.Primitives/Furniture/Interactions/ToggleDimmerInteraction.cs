using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Switch the moodlight on or off.</summary>
[GenerateSerializer, Immutable]
public sealed record ToggleDimmerInteraction : FurnitureInteraction;
