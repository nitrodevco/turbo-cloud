using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Ask a video display for its playlists and current video.</summary>
[GenerateSerializer, Immutable]
public sealed record RequestYoutubeStatusInteraction : FurnitureInteraction;
