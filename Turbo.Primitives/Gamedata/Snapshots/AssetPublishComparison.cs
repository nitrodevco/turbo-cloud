using System;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// What changed in a file a target holds an older copy of: the target's copy, read through the
/// target, against the hotel's. With the hotel's bundle row (where it came from, its revision and
/// when it last changed) and the client build stamp each copy was packed from, so staff see when
/// and why it changed; a copy whose only difference is that stamp is <see cref="AssetCompareResult.StampOnly"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetPublishComparison
{
    [Id(0)]
    public required AssetBundleFile File { get; init; }

    [Id(1)]
    public required AssetCompareResult Result { get; init; }

    /// <summary>Why the target's copy could not be compared, for <see cref="AssetCompareResult.Unreadable"/>.</summary>
    [Id(2)]
    public string? Problem { get; init; }

    /// <summary>The size of the target's copy, once read.</summary>
    [Id(3)]
    public long? TargetBytes { get; init; }

    /// <summary>
    /// Whether the target's copy is the one recorded as sent to it; false when it was changed there
    /// since (or by something else).
    /// </summary>
    [Id(4)]
    public bool TargetAsRecorded { get; init; }

    /// <summary>Where the hotel's copy came from; null when it has no bundle row.</summary>
    [Id(5)]
    public AssetBundleSource? Source { get; init; }

    [Id(6)]
    public string? Revision { get; init; }

    /// <summary>When the hotel's copy last changed.</summary>
    [Id(7)]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>The client build stamp the target's copy was packed from, when it has one.</summary>
    [Id(8)]
    public string? StampBefore { get; init; }

    /// <summary>The client build stamp the hotel's copy was packed from, when it has one.</summary>
    [Id(9)]
    public string? StampAfter { get; init; }

    [Id(10)]
    public ImmutableArray<AssetWindowDiff> Windows { get; init; } = [];

    [Id(11)]
    public ImmutableArray<AssetTextDiff> Texts { get; init; } = [];

    /// <summary>The other files that differ, up to a limit: <see cref="EntriesChanged"/> counts them all.</summary>
    [Id(12)]
    public ImmutableArray<AssetEntryDiff> Entries { get; init; } = [];

    [Id(13)]
    public int EntriesChanged { get; init; }
}
