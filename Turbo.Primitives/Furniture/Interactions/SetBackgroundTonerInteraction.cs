using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Set the room background colour.</summary>
[GenerateSerializer, Immutable]
public sealed record SetBackgroundTonerInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int Hue { get; init; }

    [Id(1)]
    public required int Saturation { get; init; }

    [Id(2)]
    public required int Lightness { get; init; }
}
