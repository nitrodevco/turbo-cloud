using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Switch a video display to a playlist.</summary>
[GenerateSerializer, Immutable]
public sealed record SetYoutubePlaylistInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string PlaylistId { get; init; }
}
