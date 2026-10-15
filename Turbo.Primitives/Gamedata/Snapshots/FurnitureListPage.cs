using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of furniture definitions, and how many of those searched carry each tag.</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureListPage
{
    [Id(0)]
    public required ImmutableArray<FurnitureListItem> Items { get; init; }

    /// <summary>Definitions found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    [Id(2)]
    public required int PageSize { get; init; }

    /// <summary>
    /// Of the definitions the words and kind find (whichever tag is asked for), how many carry each
    /// tag; a tag none carries is left out.
    /// </summary>
    [Id(3)]
    public required ImmutableDictionary<FurnitureTag, int> TagCounts { get; init; }

    /// <summary>Of the same, how many there are in all.</summary>
    [Id(4)]
    public required int Searched { get; init; }
}
