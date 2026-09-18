using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Pin a note to a post-it wall.</summary>
[GenerateSerializer, Immutable]
public sealed record AddSpamWallPostItInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string Location { get; init; }

    [Id(1)]
    public required string Color { get; init; }

    [Id(2)]
    public required string Text { get; init; }
}
