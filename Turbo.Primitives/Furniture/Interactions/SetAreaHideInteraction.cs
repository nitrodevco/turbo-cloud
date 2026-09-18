using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Configure the area a hider covers.</summary>
[GenerateSerializer, Immutable]
public sealed record SetAreaHideInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int RootX { get; init; }

    [Id(1)]
    public required int RootY { get; init; }

    [Id(2)]
    public required int Width { get; init; }

    [Id(3)]
    public required int Length { get; init; }

    [Id(4)]
    public required bool Invisibility { get; init; }

    [Id(5)]
    public required bool WallItems { get; init; }

    [Id(6)]
    public required bool Invert { get; init; }
}
