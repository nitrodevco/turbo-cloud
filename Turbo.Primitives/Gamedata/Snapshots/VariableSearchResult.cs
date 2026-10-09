using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of external variables found by key or value.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableSearchResult
{
    [Id(0)]
    public required ImmutableArray<VariableEntrySnapshot> Items { get; init; }

    /// <summary>Variables found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    /// <summary>Variables a page holds.</summary>
    [Id(2)]
    public required int PageSize { get; init; }

    /// <summary>The files a variable may follow the address of (<see cref="GamedataFiles"/>).</summary>
    [Id(3)]
    public required ImmutableArray<string> LinkableFiles { get; init; }

    /// <summary>
    /// Whether a variable following a file is written with its address by hash: false while the
    /// gamedata host has no public address, when it is written with its own value.
    /// </summary>
    [Id(4)]
    public required bool WritesAddresses { get; init; }
}
