using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A palette: its colours, in the order the editor offers them, and the kinds of clothing coloured from it.</summary>
[GenerateSerializer, Immutable]
public sealed record FigurePaletteSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>The kinds of clothing (<c>hr</c>, <c>ch</c>...) whose palette it is.</summary>
    [Id(1)]
    public required ImmutableArray<string> UsedBy { get; init; }

    [Id(2)]
    public required ImmutableArray<FigureEntrySnapshot> Colors { get; init; }
}
