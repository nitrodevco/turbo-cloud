using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Open the moodlight editor: the item answers with its presets.</summary>
[GenerateSerializer, Immutable]
public sealed record RequestDimmerPresetsInteraction : FurnitureInteraction;
