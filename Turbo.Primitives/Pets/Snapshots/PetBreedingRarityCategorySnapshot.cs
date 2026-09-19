using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>Odds of one rarity category in a nest breeding and the palettes it can yield.</summary>
[GenerateSerializer, Immutable]
public sealed record PetBreedingRarityCategorySnapshot
{
    [Id(0)]
    public required int RarityLevel { get; init; }

    /// <summary>Chance in percent.</summary>
    [Id(1)]
    public required int Chance { get; init; }

    [Id(2)]
    public required ImmutableArray<int> PaletteIds { get; init; }
}
