using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of the bundles, by kind and name.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetBundlePage
{
    [Id(0)]
    public required ImmutableArray<AssetBundleSnapshot> Items { get; init; }

    /// <summary>Bundles found, on every page.</summary>
    [Id(1)]
    public required int Total { get; init; }

    /// <summary>Bundles a page holds.</summary>
    [Id(2)]
    public required int PageSize { get; init; }
}
