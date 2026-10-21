using System;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// How many bundles of a kind the hotel has with a file, how many failed, their size, and when one
/// was last downloaded from Habbo.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetKindSummary
{
    [Id(0)]
    public required AssetBundleKind Kind { get; init; }

    /// <summary>Bundles with a file.</summary>
    [Id(1)]
    public required int Bundles { get; init; }

    /// <summary>Libraries that could not be downloaded or converted.</summary>
    [Id(2)]
    public required int Failed { get; init; }

    /// <summary>The files' size together, in bytes.</summary>
    [Id(3)]
    public required long Bytes { get; init; }

    /// <summary>When a bundle of it was last downloaded from Habbo; null when none was.</summary>
    [Id(4)]
    public DateTime? UpdatedAt { get; init; }
}
