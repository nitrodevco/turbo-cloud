using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Store a moodlight preset and optionally switch to it.</summary>
[GenerateSerializer, Immutable]
public sealed record SaveDimmerPresetInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int PresetId { get; init; }

    [Id(1)]
    public required int EffectType { get; init; }

    [Id(2)]
    public required string Color { get; init; }

    [Id(3)]
    public required int Brightness { get; init; }

    [Id(4)]
    public required bool Apply { get; init; }
}
