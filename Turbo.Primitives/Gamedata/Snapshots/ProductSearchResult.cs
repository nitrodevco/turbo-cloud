using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of products found by code, name or description.</summary>
[GenerateSerializer, Immutable]
public sealed record ProductSearchResult
{
    [Id(0)]
    public required ImmutableArray<ProductEntrySnapshot> Items { get; init; }

    /// <summary>Products found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    [Id(2)]
    public required int PageSize { get; init; }
}
