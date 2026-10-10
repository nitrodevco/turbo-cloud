using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A file inside a bundle's zip, and its size unpacked.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetBundleEntry
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required long Size { get; init; }
}
