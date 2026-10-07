using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>The kinds of clothing, each with how many pieces it has, and the id a new piece takes.</summary>
[GenerateSerializer, Immutable]
public sealed record FigureKindsSnapshot
{
    [Id(0)]
    public required ImmutableArray<FigureKindSnapshot> Kinds { get; init; }

    /// <summary>One past the highest piece id: free in every kind, as a piece's id is.</summary>
    [Id(1)]
    public required int NextSetId { get; init; }
}

/// <summary>A kind of clothing, and how many pieces it has.</summary>
[GenerateSerializer, Immutable]
public sealed record FigureKindSnapshot
{
    [Id(0)]
    public required FigureEntrySnapshot Entry { get; init; }

    [Id(1)]
    public required int Pieces { get; init; }
}
