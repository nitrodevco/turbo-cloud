using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of texts found by key or value.</summary>
[GenerateSerializer, Immutable]
public sealed record TextSearchResult
{
    [Id(0)]
    public required ImmutableArray<TextEntrySnapshot> Items { get; init; }

    /// <summary>Texts found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    /// <summary>Texts a page holds.</summary>
    [Id(2)]
    public required int PageSize { get; init; }
}
