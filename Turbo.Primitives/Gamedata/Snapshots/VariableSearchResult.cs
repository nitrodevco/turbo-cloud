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

    /// <summary>
    /// The variables the hotel writes itself, each the address of one of its gamedata files by
    /// hash, as the file has them now. Staff can't set these. Empty when the gamedata host has no
    /// public address to write them on.
    /// </summary>
    [Id(3)]
    public required ImmutableArray<VariableEntrySnapshot> Stamped { get; init; }
}
