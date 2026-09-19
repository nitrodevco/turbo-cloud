using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>
/// The pet figure struct as pet packets carry it. <see cref="PaletteId"/> and
/// <see cref="BreedId"/> are the same value for a pet bought from the catalog: the breed picks
/// the palette the renderer uses and the breed name the info stand shows.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PetFigureSnapshot
{
    [Id(0)]
    public required int TypeId { get; init; }

    [Id(1)]
    public required int PaletteId { get; init; }

    /// <summary>Hex colour without <c>#</c>.</summary>
    [Id(2)]
    public required string Color { get; init; }

    [Id(3)]
    public required int BreedId { get; init; }

    /// <summary>Flat triples of layer id, part id, palette id.</summary>
    [Id(4)]
    public required ImmutableArray<int> CustomParts { get; init; }
}
