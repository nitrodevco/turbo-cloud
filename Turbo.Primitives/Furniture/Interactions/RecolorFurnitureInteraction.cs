using Orleans;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Recolor a recolorable furni, and with it the others the scope names.</summary>
[GenerateSerializer, Immutable]
public sealed record RecolorFurnitureInteraction : FurnitureInteraction
{
    [Id(0)]
    public required RecolorScopeType Scope { get; init; }

    [Id(1)]
    public required int Red { get; init; }

    [Id(2)]
    public required int Green { get; init; }

    [Id(3)]
    public required int Blue { get; init; }
}
