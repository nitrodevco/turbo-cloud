using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Nudge a stack tile to the next item height above or below.</summary>
[GenerateSerializer, Immutable]
public sealed record SetAdjacentStackHeightInteraction : FurnitureInteraction
{
    [Id(0)]
    public required bool Down { get; init; }
}
