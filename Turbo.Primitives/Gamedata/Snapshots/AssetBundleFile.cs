using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A bundle the store has a file for: its kind and name, its path under the store's folder (and
/// under a publish target's), and its hash and size.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetBundleFile(
    [property: Id(0)] AssetBundleKind Kind,
    [property: Id(1)] string Name,
    [property: Id(2)] string Path,
    [property: Id(3)] string Hash,
    [property: Id(4)] long Size
);
