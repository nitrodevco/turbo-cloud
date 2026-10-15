using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// What an upload was found to be: a library, kept as a bundle of the kind its content says, or a
/// gamedata file, recognised but not taken (the hotel builds its gamedata from the database).
/// Exactly one of the two is set.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetUploadResult
{
    [Id(0)]
    public AssetBundleSnapshot? Bundle { get; init; }

    [Id(1)]
    public GamedataFileKind? Gamedata { get; init; }
}
