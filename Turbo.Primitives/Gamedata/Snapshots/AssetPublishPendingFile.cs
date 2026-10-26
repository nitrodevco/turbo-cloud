using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A bundle a publish to a target would send, and why.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetPublishPendingFile(
    [property: Id(0)] AssetBundleFile File,
    [property: Id(1)] AssetPublishChange Change
);
