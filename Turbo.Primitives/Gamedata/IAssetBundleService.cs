using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The bundles the hotel keeps, as the admin panel works with them: counted, listed, opened,
/// uploaded and deleted, and checked against the hotel's furniture, effects and pets.
/// </summary>
public interface IAssetBundleService
{
    /// <summary>The largest file an upload may be, in bytes.</summary>
    long UploadMaxBytes { get; }

    Task<AssetOverview> GetOverviewAsync(CancellationToken ct);

    /// <summary>
    /// A page (from 0) of the bundles of <paramref name="kind"/> (every kind when null) whose name
    /// holds <paramref name="query"/>, or whose ids include it, in <paramref name="status"/>.
    /// </summary>
    Task<AssetBundlePage> ListAsync(
        AssetBundleKind? kind,
        string? query,
        AssetBundleStatusFilter status,
        int page,
        CancellationToken ct
    );

    /// <summary>A bundle and what its zip holds; null when there is none.</summary>
    Task<AssetBundleDetail?> GetAsync(AssetBundleKind kind, string name, CancellationToken ct);

    /// <summary>A bundle's file on this server; null when it has none.</summary>
    Task<string?> GetFilePathAsync(AssetBundleKind kind, string name, CancellationToken ct);

    /// <summary>Deletes a bundle's row and file; false when there was none.</summary>
    Task<bool> DeleteAsync(
        AssetBundleKind kind,
        string name,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Keeps <paramref name="data"/> as an uploaded bundle: an SWF or a <c>.hab</c> converted, a
    /// <c>.nitro</c> as it is once it reads. <paramref name="name"/> defaults to the file's name
    /// without its extension. Throws <see cref="System.ArgumentException"/>, with why, for a name,
    /// file or size it can't take.
    /// </summary>
    Task<AssetBundleSnapshot> UploadAsync(
        AssetBundleKind kind,
        string? name,
        string fileName,
        byte[] data,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Takes a file whatever it is, found by its content: a library (an SWF, a <c>.hab</c> or a
    /// <c>.nitro</c>) is kept as an uploaded bundle of the kind its asset data says - furniture,
    /// a pet, an effect or clothing, or, when the data says none, the kind a bundle of its name
    /// already is; a gamedata file is recognised and named, not taken. <paramref name="name"/>
    /// defaults to the file's name without its extension. Throws
    /// <see cref="AssetKindUnknownException"/> when the content says what it is no way, and
    /// <see cref="System.ArgumentException"/>, with why, for a name, file or size it can't take.
    /// </summary>
    Task<AssetUploadResult> UploadFileAsync(
        string? name,
        string fileName,
        byte[] data,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Every check of the bundles against the hotel, in a fixed order.</summary>
    Task<ImmutableArray<AssetCheckSnapshot>> GetChecksAsync(CancellationToken ct);
}
