using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Set a stack tile height in hundredths of a tile; -100 means above the stack.</summary>
[GenerateSerializer, Immutable]
public sealed record SetStackHeightInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int Height { get; init; }
}
