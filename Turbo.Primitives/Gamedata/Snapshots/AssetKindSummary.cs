using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>How many bundles of a kind the hotel has with a file, how many failed, and their size.</summary>
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
}
