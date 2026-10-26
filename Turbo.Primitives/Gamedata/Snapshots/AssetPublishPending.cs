using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// What a target lacks or holds an older copy of, by what it recorded: a page of the files whose
/// path holds the search, largest first as a publish sends them, and the totals of all of them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetPublishPending
{
    [Id(0)]
    public required ImmutableArray<AssetPublishPendingFile> Items { get; init; }

    /// <summary>Files the search found, on every page.</summary>
    [Id(1)]
    public required int Matched { get; init; }

    /// <summary>Files a page holds.</summary>
    [Id(2)]
    public required int PageSize { get; init; }

    /// <summary>Every file pending, whatever the search: the target's <c>Pending</c>.</summary>
    [Id(3)]
    public required int Count { get; init; }

    /// <summary>The size of every file pending, in bytes.</summary>
    [Id(4)]
    public required long Bytes { get; init; }

    /// <summary>Files pending that were never sent to the target.</summary>
    [Id(5)]
    public required int New { get; init; }

    /// <summary>Files pending whose copy changed since they were sent.</summary>
    [Id(6)]
    public required int Changed { get; init; }
}
