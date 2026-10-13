using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>A label's new text, typed into its bubble (<c>SetFurnitureLabelData</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record SetFurnitureLabelTextInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string Text { get; init; }
}
