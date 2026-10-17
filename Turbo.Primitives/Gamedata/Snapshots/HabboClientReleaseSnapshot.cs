using System;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A release of Habbo's HTML client the hotel downloaded and checked: its version, where it came
/// from, when it was built and taken, which environments served it, and how many files its
/// manifest lists.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboClientReleaseSnapshot
{
    [Id(0)]
    public required string Version { get; init; }

    [Id(1)]
    public required string ReleaseId { get; init; }

    [Id(2)]
    public required string Url { get; init; }

    [Id(3)]
    public required DateTime DownloadedAt { get; init; }

    /// <summary>When Habbo built it, as its launcher says; null when it does not.</summary>
    [Id(4)]
    public DateTime? BuiltAt { get; init; }

    [Id(5)]
    public required ImmutableArray<string> Environments { get; init; }

    [Id(6)]
    public required int FileCount { get; init; }

    /// <summary>The <c>.hab</c> libraries in it, by path.</summary>
    [Id(7)]
    public required ImmutableArray<string> Libraries { get; init; }

    [Id(8)]
    public required long Size { get; init; }
}
