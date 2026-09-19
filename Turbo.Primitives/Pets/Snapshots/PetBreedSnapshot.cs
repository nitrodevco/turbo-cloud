using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>A breed (palette) of a pet type: what the catalog may sell and what breeding can produce.</summary>
[GenerateSerializer, Immutable]
public sealed record PetBreedSnapshot
{
    [Id(0)]
    public required int TypeId { get; init; }

    [Id(1)]
    public required int BreedId { get; init; }

    [Id(2)]
    public required int PaletteId { get; init; }

    /// <summary>Rarity category used by breeding odds and shown as the pet's rarity level.</summary>
    [Id(3)]
    public required int RarityLevel { get; init; }

    [Id(4)]
    public required bool Sellable { get; init; }

    [Id(5)]
    public required bool Rare { get; init; }

    /// <summary>The asset's colour tag of this palette; -1 when the palette carries none. Dyes pick by it.</summary>
    [Id(6)]
    public required int ColorTag { get; init; }
}
