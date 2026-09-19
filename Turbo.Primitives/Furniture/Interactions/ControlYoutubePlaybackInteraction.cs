using Orleans;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Pause, play or skip on a video display.</summary>
[GenerateSerializer, Immutable]
public sealed record ControlYoutubePlaybackInteraction : FurnitureInteraction
{
    [Id(0)]
    public required YoutubePlaybackCommandType Command { get; init; }
}
