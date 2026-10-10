using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A check of the bundles against the hotel: what it looks for, how many it found and some of
/// them. <see cref="Kind"/> and <see cref="Status"/>, when set, are the bundle list it is about.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetCheckSnapshot
{
    /// <summary>Its id: <c>furniture-missing</c>, <c>file-missing</c>, ...</summary>
    [Id(0)]
    public required string Id { get; init; }

    [Id(1)]
    public required AssetCheckSeverity Severity { get; init; }

    [Id(2)]
    public required string Title { get; init; }

    /// <summary>What it found means, in a sentence.</summary>
    [Id(3)]
    public required string Detail { get; init; }

    [Id(4)]
    public required int Count { get; init; }

    /// <summary>Up to the configured limit of what it found.</summary>
    [Id(5)]
    public required ImmutableArray<string> Samples { get; init; }

    [Id(6)]
    public AssetBundleKind? Kind { get; init; }

    [Id(7)]
    public AssetBundleStatusFilter? Status { get; init; }
}
