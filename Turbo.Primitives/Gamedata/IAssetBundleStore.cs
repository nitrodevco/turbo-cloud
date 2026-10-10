using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Where the bundles are kept: a folder laid out as an asset host serves it
/// (<c>bundled/furniture/&lt;name&gt;.nitro</c>, <c>bundled/effects/...</c>, <c>bundled/pet/...</c>),
/// with a row per bundle in <c>asset_bundles</c> saying what is known of it. Files are written
/// whole or not at all, so a reader never sees half of one.
/// </summary>
public interface IAssetBundleStore
{
    /// <summary>The folder, as a full path.</summary>
    string Root { get; }

    /// <summary>A bundle's path under the folder, with forward slashes: <c>bundled/furniture/chair.nitro</c>.</summary>
    string PathOf(AssetBundleKind kind, string name);

    /// <summary>A bundle's file on this server.</summary>
    string FullPathOf(AssetBundleKind kind, string name);

    /// <summary>Whether a name can be a bundle's: letters, digits, <c>_ - .</c>, and no path in it.</summary>
    bool IsValidName(string name);

    /// <summary>Writes a bundle's file whole, replacing any before it; returns its SHA-1 in lowercase hex.</summary>
    Task<string> WriteAsync(AssetBundleKind kind, string name, byte[] bundle, CancellationToken ct);

    /// <summary>Removes a bundle's file, when there is one.</summary>
    void Delete(AssetBundleKind kind, string name);

    /// <summary>Every bundle with a file, as its row says, ordered by path.</summary>
    Task<IReadOnlyList<AssetBundleFile>> ListFilesAsync(CancellationToken ct);
}
