using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots;

/// <summary>A playlist a video display can be set to, as its menu lists it.</summary>
[GenerateSerializer, Immutable]
public sealed record YoutubePlaylistSnapshot
{
    [Id(0)]
    public required string PlaylistId { get; init; }

    [Id(1)]
    public required string Title { get; init; }

    [Id(2)]
    public required string Description { get; init; }
}
