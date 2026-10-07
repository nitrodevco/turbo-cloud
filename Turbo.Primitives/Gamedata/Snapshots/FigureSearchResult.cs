using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of figure records.</summary>
[GenerateSerializer, Immutable]
public sealed record FigureSearchResult
{
    [Id(0)]
    public required ImmutableArray<FigureEntrySnapshot> Items { get; init; }

    /// <summary>Records found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    [Id(2)]
    public required int PageSize { get; init; }
}
