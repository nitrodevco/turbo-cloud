using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// The comparisons asked for: one file's, or every changed file's up to a limit, largest first;
/// <see cref="Total"/> counts the changed files there are.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetPublishComparisons(
    [property: Id(0)] ImmutableArray<AssetPublishComparison> Items,
    [property: Id(1)] int Total
);
