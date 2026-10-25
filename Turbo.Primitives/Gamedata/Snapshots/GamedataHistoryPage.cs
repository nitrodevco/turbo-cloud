using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of the gamedata history, newest first, and how many sets it holds in all.</summary>
[GenerateSerializer, Immutable]
public sealed record GamedataHistoryPage
{
    [Id(0)]
    public required ImmutableArray<GamedataChangeSetSnapshot> Sets { get; init; }

    /// <summary>Sets found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    /// <summary>The page, from 0.</summary>
    [Id(2)]
    public required int Page { get; init; }

    [Id(3)]
    public required int PageSize { get; init; }
}
