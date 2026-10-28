using System;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>Shared URL and client-download conventions for discovery and sync.</summary>
internal static class HabboAssetUrls
{
    public const string CLIENT_PREFIX = "client:";

    public const string NOT_A_FILE =
        "Habbo answered with a page, not the file; its filter may have refused the request.";

    public static string Furniture(GamedataConfig config, string domain, HabboLibrary library) =>
        config
            .FurnitureFileUrl.Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{revision}", library.Revision, StringComparison.Ordinal)
            .Replace(
                "{name}",
                Uri.EscapeDataString(library.Download ?? library.Name),
                StringComparison.Ordinal
            );

    public static string File(
        AssetBundleConfig config,
        AssetBundleKind kind,
        string domain,
        string name
    ) =>
        (
            kind switch
            {
                AssetBundleKind.Badge => config.BadgeFileUrl,
                AssetBundleKind.CatalogIcon or AssetBundleKind.CatalogImage =>
                    config.CatalogFileUrl,
                AssetBundleKind.Sound => config.SoundFileUrl,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            }
        )
            .Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{name}", Uri.EscapeDataString(name), StringComparison.Ordinal);

    public static string Gordon(
        AssetBundleConfig config,
        string domain,
        string revision,
        string name
    ) =>
        config
            .GordonFileUrl.Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{revision}", revision, StringComparison.Ordinal)
            .Replace("{name}", Uri.EscapeDataString(name), StringComparison.Ordinal);
}
