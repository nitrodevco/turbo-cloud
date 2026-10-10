using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A bundle, where it is kept, and what its zip holds (nothing while it has no file).</summary>
[GenerateSerializer, Immutable]
public sealed record AssetBundleDetail
{
    [Id(0)]
    public required AssetBundleSnapshot Bundle { get; init; }

    /// <summary>Its path under the bundle folder: <c>bundled/furniture/chair.nitro</c>.</summary>
    [Id(1)]
    public required string Path { get; init; }

    [Id(2)]
    public required ImmutableArray<AssetBundleEntry> Files { get; init; }
}
