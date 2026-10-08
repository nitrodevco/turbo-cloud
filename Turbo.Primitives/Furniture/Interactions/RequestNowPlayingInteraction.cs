using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Ask the room's jukebox what it is playing and how far in, to join in at that point.</summary>
[GenerateSerializer, Immutable]
public sealed record RequestNowPlayingInteraction : FurnitureInteraction;
