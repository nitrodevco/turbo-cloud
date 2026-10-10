using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>The bundles at a glance: where they are kept, each kind's counts, and the checks that found something.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetOverview
{
    /// <summary>The bundle folder, as a full path.</summary>
    [Id(0)]
    public required string Directory { get; init; }

    /// <summary>One per kind, in the kinds' order.</summary>
    [Id(1)]
    public required ImmutableArray<AssetKindSummary> Kinds { get; init; }

    /// <summary>Checks of error severity that found something.</summary>
    [Id(2)]
    public required int Errors { get; init; }

    /// <summary>Checks of warning severity that found something.</summary>
    [Id(3)]
    public required int Warnings { get; init; }

    /// <summary>Publish targets set up.</summary>
    [Id(4)]
    public required int Targets { get; init; }
}
