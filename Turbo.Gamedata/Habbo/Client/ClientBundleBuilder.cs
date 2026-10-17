using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Assets.Hab;
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Habbo.Client;

/// <summary>
/// The client libraries nitro-studio builds from a release of Habbo's HTML client rather than
/// converting them, built here from the release the hotel follows and kept in the bundle store as
/// templates:
/// <list type="bullet">
/// <item><c>habbo-avatar-render-lib</c> (<c>server/avatar-data.ts</c>): the avatar additions (the
/// typing and muted bubbles, the idle Zs, the numbers) on one sheet under the names the renderer asks
/// for them by, and the tables it starts from - geometry, part sets, the placeholder figure, the
/// built-in animations, the action offsets and the animations - converted from the library's XML;</item>
/// <item><c>habbo-room-object-visualization-lib</c> (<c>server/renderer-assets.ts</c>): the Variable
/// FX bitmaps on one sheet, with <c>variable-fx-tables.json</c> - each icon's offset and the renderer
/// each id draws with.</item>
/// </list>
/// A bundle is stamped with the library's hash, so a release that leaves a library as it was builds
/// nothing again; one the hotel uploaded itself is left alone.
/// </summary>
internal sealed partial class ClientBundleBuilder(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    HabboClientService client,
    TimeProvider time,
    ILogger<ClientBundleBuilder> logger
)
{
    public const string AVATAR_LIBRARY = "habbo-avatar-render-lib";
    public const string VISUALIZATION_LIBRARY = "habbo-room-object-visualization-lib";
    public const string VARIABLE_FX_TABLES = "variable-fx-tables.json";

    /// <summary>Changed when a bundle is built differently, so every one is built again.</summary>
    private const string BUILD = "1";

    /// <summary>A template bundle's revision: this, its build, and the hash of the release's libraries.</summary>
    private const string TEMPLATES_PREFIX = "templates:";

    /// <summary>Changed when the templates are packed differently, so every one is built again.</summary>
    private const string TEMPLATES_BUILD = "1";

    /// <summary>The libraries with a bundle of their own, built from what the hotel keeps of them: never packed as templates.</summary>
    private static readonly HashSet<string> OWN_BUILDS = new(StringComparer.Ordinal)
    {
        AVATAR_LIBRARY,
        VISUALIZATION_LIBRARY,
        ChatStyles.ChatStyleService.LIBRARY,
    };

    /// <summary>The stamp of an avatar render bundle built without Habbo's avatar actions.</summary>
    private const string NO_ACTIONS = "no-actions";

    private const string VARIABLE_FX_PREFIX = "variablefx_";

    /// <summary>The built-in animations, by their asset in the avatar render library.</summary>
    private static readonly string[] BUILT_IN_ANIMATIONS = ["dance_sixseven_animation"];

    /// <summary>The action offsets, by action and their asset in the avatar render library.</summary>
    private static readonly (string Action, string Asset)[] ACTION_OFFSETS =
    [
        ("lay", "action_offset_lay"),
        ("swim", "action_offset_swim"),
    ];

    private static readonly JsonSerializerOptions JSON = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Builds every bundle whose library changed in the followed release; how many were built.
    /// <paramref name="avatarActions"/> is Habbo's <c>HabboAvatarActions.xml</c>, which the avatar
    /// render library's bundle carries as <c>actions.json</c>; without it (Habbo could not be asked) a
    /// bundle built with it before is kept, as the avatars would only stand without one.
    /// </summary>
    public async Task<int> BuildAsync(
        byte[]? avatarActions,
        Action<string> log,
        CancellationToken ct
    )
    {
        var actions = avatarActions is null ? null : Encoding.UTF8.GetString(avatarActions);
        var actionsStamp = avatarActions is null
            ? NO_ACTIONS
            : Convert.ToHexStringLower(SHA256.HashData(avatarActions))[..16];
        var builds = new (
            string Library,
            string Stamp,
            Func<HabLibrary, Dictionary<string, byte[]>> Build
        )[]
        {
            (AVATAR_LIBRARY, actionsStamp, hab => AvatarRender(hab, actions)),
            (VISUALIZATION_LIBRARY, string.Empty, Visualization),
        };

        var release = await client.OpenFollowedAsync(ct).ConfigureAwait(false);

        if (release is null)
        {
            log(
                "No release of Habbo's HTML client is downloaded: the client's own libraries are not built."
            );

            return 0;
        }

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);
        var built = 0;

        foreach (var (library, stamp, build) in builds)
        {
            var path = $"generated/{library}.hab";
            var row = await dbCtx
                .AssetBundles.FirstOrDefaultAsync(
                    x => x.Kind == AssetBundleKind.Template && x.Name == library,
                    ct
                )
                .ConfigureAwait(false);

            if (row?.Source == AssetBundleSource.Upload)
            {
                log($"{library} is the hotel's own upload: left as it is.");

                continue;
            }

            if (
                !release.FileHashes.TryGetValue(path, out var sha256)
                || release.Read(path) is not { } data
            )
            {
                log($"Habbo's HTML client {release.Version} has no {path}.");

                continue;
            }

            // Half the library's hash, as the templates have it: the revision column holds 64.
            var revision =
                $"client:{BUILD}:{sha256[..Math.Min(sha256.Length, 32)]}{(stamp.Length > 0 ? ":" + stamp : "")}";

            if (row?.Revision == revision && row.Error is null)
                continue;

            if (
                stamp == NO_ACTIONS
                && row?.Source == AssetBundleSource.Built
                && row.Revision?.EndsWith(":" + NO_ACTIONS, StringComparison.Ordinal) == false
            )
            {
                log($"{library} kept as it is: Habbo's avatar actions could not be read.");

                continue;
            }

            Dictionary<string, byte[]> files;

            try
            {
                files = build(HabLibrary.Read(data, library));
            }
            catch (Exception ex) when (ex is AssetFormatException or FormatException)
            {
                logger.LogWarning(
                    ex,
                    "{Library} could not be built from Habbo's HTML client {Version}",
                    library,
                    release.Version
                );
                log(
                    $"{library} could not be built from Habbo's HTML client {release.Version}: {ex.Message}"
                );

                continue;
            }

            await SaveAsync(dbCtx, row, library, files, revision, release.Version, ct)
                .ConfigureAwait(false);
            built++;
            log($"Built {library} from Habbo's HTML client {release.Version}.");
        }

        built += await TemplatesAsync(dbCtx, release, log, ct).ConfigureAwait(false);

        return built;
    }

    /// <summary>
    /// The window template bundles (<see cref="ClientTemplates"/>): one for each library of the
    /// release with a <c>&lt;layout&gt;</c>, and the window manager's, which every one draws from. All
    /// are stamped with the release's libraries, so they are built again, together, when one changes;
    /// a bundle of a library the release no longer has is taken away, and an uploaded one left alone.
    /// </summary>
    private async Task<int> TemplatesAsync(
        TurboDbContext dbCtx,
        HabboClientRelease release,
        Action<string> log,
        CancellationToken ct
    )
    {
        var habs = release
            .FileHashes.Where(x => x.Key.EndsWith(".hab", StringComparison.Ordinal))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        var revision =
            $"{TEMPLATES_PREFIX}{TEMPLATES_BUILD}:"
            + Convert.ToHexStringLower(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        string.Join("\n", habs.Select(x => $"{x.Key}={x.Value}"))
                    )
                )
            )[..32];
        var rows = await dbCtx
            .AssetBundles.Where(x => x.Kind == AssetBundleKind.Template)
            .ToDictionaryAsync(x => x.Name, ct)
            .ConfigureAwait(false);
        var ours = rows
            .Values.Where(x =>
                x.Source == AssetBundleSource.Built
                && x.Revision?.StartsWith(TEMPLATES_PREFIX, StringComparison.Ordinal) == true
            )
            .ToList();

        if (ours.Count > 0 && ours.All(x => x.Revision == revision && x.Error is null))
            return 0;

        var libraries = new List<(string Name, HabLibrary Hab)>();
        HabLibrary? windowManager = null;

        foreach (var (path, _) in habs)
        {
            var name = LibraryNamePattern().Replace(path, "");

            if (OWN_BUILDS.Contains(name) || release.Read(path) is not { } data)
                continue;

            HabLibrary hab;

            try
            {
                hab = HabLibrary.Read(data, name);
            }
            catch (AssetFormatException ex)
            {
                log($"{path} could not be read: {ex.Message}");

                continue;
            }

            if (name == ClientTemplates.WINDOW_MANAGER)
                windowManager = hab;

            if (name == ClientTemplates.WINDOW_MANAGER || ClientTemplates.HasLayouts(hab))
                libraries.Add((name, hab));
        }

        if (windowManager is null)
        {
            log(
                $"Habbo's HTML client {release.Version} has no window manager library: the templates are not built."
            );

            return 0;
        }

        var packed = ClientTemplates.Pack(release.Version, libraries, windowManager);
        var built = 0;

        if (
            packed.Bundles.GetValueOrDefault(ClientTemplates.WINDOW_MANAGER) is
            { } windowManagerFiles
        )
            WithTheme(windowManagerFiles, windowManager, log);

        foreach (var (library, files) in packed.Bundles)
        {
            var row = rows.GetValueOrDefault(library);

            if (row?.Source == AssetBundleSource.Upload || !store.IsValidName(library))
                continue;

            await SaveAsync(dbCtx, row, library, files, revision, release.Version, ct)
                .ConfigureAwait(false);
            built++;
        }

        foreach (var gone in ours.Where(x => !packed.Bundles.ContainsKey(x.Name)))
        {
            store.Delete(AssetBundleKind.Template, gone.Name);
            dbCtx.AssetBundles.Remove(gone);
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var problem in packed.Problems)
            log($"Template problem: {problem}");

        log(
            $"Built {built} template bundles from Habbo's HTML client {release.Version}; {packed.Problems.Count} problems."
        );

        return built;
    }

    /// <summary>
    /// The UI theme (<see cref="Theme.ClientTheme"/>) put into the window manager's bundle, as
    /// nitro-studio's <c>withTheme</c> puts it: its sheet, the sheet's frames and
    /// <c>theme-variants.json</c> beside the templates, and its sprites named in the asset data.
    /// </summary>
    private void WithTheme(
        Dictionary<string, byte[]> files,
        HabLibrary windowManager,
        Action<string> log
    )
    {
        Theme.ClientThemeFiles? theme;

        try
        {
            theme = Theme.ClientTheme.Build(windowManager);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(
                ex,
                "The UI theme could not be built from the window manager library"
            );
            log($"The UI theme could not be built: {ex.Message}");

            return;
        }

        if (theme is null)
            return;

        var assetFile = $"{ClientTemplates.WINDOW_MANAGER}.json";
        var assetData = JsonNode.Parse(files[assetFile])!.AsObject();
        var assets = new JsonArray([
            .. (assetData["assets"] as JsonArray ?? [])
                .Where(x =>
                    x?["name"]?.GetValue<string>().StartsWith("theme-", StringComparison.Ordinal)
                    == false
                )
                .Select(x => x!.DeepClone()),
            .. theme.AssetNames.Select(x => (JsonNode)new JsonObject { ["name"] = x }),
        ]);

        assetData["assets"] = assets;
        files[assetFile] = Json(assetData);

        foreach (var (name, data) in theme.Files)
            files[name] = data;
    }

    /// <summary>A bundle written through the store, and its row kept as one the hotel built.</summary>
    private async Task SaveAsync(
        TurboDbContext dbCtx,
        AssetBundleEntity? row,
        string library,
        Dictionary<string, byte[]> files,
        string revision,
        string version,
        CancellationToken ct
    )
    {
        var bundle = NitroBundle.Create(files).Write();
        var hash = await store
            .WriteAsync(AssetBundleKind.Template, library, bundle, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AssetBundleEntity
            {
                Kind = AssetBundleKind.Template,
                Name = library,
                Source = AssetBundleSource.Built,
            };
            dbCtx.AssetBundles.Add(row);
        }

        row.Source = AssetBundleSource.Built;
        row.Revision = revision;
        row.Hash = hash;
        row.Size = bundle.LongLength;
        row.Error = null;
        row.Retry = false;
        row.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "Built {Library} from Habbo's HTML client {Version}: {Bytes} bytes ({Hash})",
            library,
            version,
            bundle.LongLength,
            hash
        );
    }

    /// <summary>
    /// The avatar render library's bundle: its avatar additions (<c>user_typing_png</c> as
    /// <c>avatar_addition_user_typing</c>) and the tables. Its <c>h_std_*</c> bitmaps are a default
    /// figure's parts, which the figure libraries carry.
    /// </summary>
    internal static Dictionary<string, byte[]> AvatarRender(HabLibrary hab, string? actions)
    {
        var additions = hab
            .Entries.Where(x =>
                x.MimeType == "image/png" && x.Name.EndsWith("_png", StringComparison.Ordinal)
            )
            .Select(x => ($"avatar_addition_{x.Name[..^4]}", hab.Get(x)))
            .ToList();
        var files =
            additions.Count > 0
                ? Atlas(AVATAR_LIBRARY, additions)
                : new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [$"{AVATAR_LIBRARY}.json"] = Json(new JsonObject { ["type"] = AVATAR_LIBRARY }),
                };

        string Xml(string name) =>
            hab.GetXml(name)
            ?? throw new FormatException($"The avatar render library has no {name}.");

        files["geometry.json"] = Json(AvatarTablesMapper.Geometry(Xml("HabboAvatarGeometry")));
        files["part-sets.json"] = Json(AvatarTablesMapper.PartSets(Xml("HabboAvatarPartSets")));
        files["figure-data.json"] = FigureDataFile.Write(
            FigureDataFile.Parse(Encoding.UTF8.GetBytes(Xml("HabboAvatarFigure")))
        );
        files["built-in-animations.json"] = Json(
            new JsonArray([
                .. BUILT_IN_ANIMATIONS.Select(x =>
                    (JsonNode)AvatarTablesMapper.BuiltInAnimation(Xml(x))
                ),
            ])
        );
        files["action-offsets.json"] = Json(
            new JsonArray([
                .. ACTION_OFFSETS.Select(x =>
                    (JsonNode)AvatarTablesMapper.ActionOffsets(x.Action, Xml(x.Asset))
                ),
            ])
        );

        if (actions is not null)
            files["actions.json"] = Json(AvatarTablesMapper.Actions(actions));

        if (hab.GetXml("HabboAvatarAnimation") is { } animations)
            files["animations.json"] = Json(AvatarTablesMapper.Animations(animations));

        return files;
    }

    /// <summary>The room object visualization library's bundle: its Variable FX bitmaps and their tables.</summary>
    internal static Dictionary<string, byte[]> Visualization(HabLibrary hab)
    {
        var bitmaps = hab
            .Entries.Where(x =>
                x.MimeType == "image/png"
                && x.Name.StartsWith(VARIABLE_FX_PREFIX, StringComparison.Ordinal)
            )
            .Select(x => (x.Name, hab.Get(x)))
            .ToList();

        if (bitmaps.Count == 0)
            throw new FormatException(
                "The room object visualization library has no Variable FX bitmaps."
            );

        var icons = new JsonObject();

        foreach (
            Match match in IconPattern()
                .Matches(Uncommented(hab.GetXml("variablefx_icon_metadata")))
        )
            icons[match.Groups[1].Value] = new JsonObject
            {
                ["x"] = int.Parse(match.Groups[2].Value),
                ["y"] = int.Parse(match.Groups[3].Value),
            };

        var renderers = new JsonArray([
            .. RendererPattern()
                .Matches(Uncommented(hab.GetXml("variablefx_renderer_mapping")))
                .Select(match =>
                    (JsonNode)
                        new JsonObject
                        {
                            ["id"] = int.Parse(match.Groups[1].Value),
                            ["name"] = match.Groups[2].Value,
                            ["rendererClass"] = match.Groups[3].Value,
                        }
                ),
        ]);
        var files = Atlas(VISUALIZATION_LIBRARY, bitmaps);

        files[VARIABLE_FX_TABLES] = Json(
            new JsonObject { ["icons"] = icons, ["renderers"] = renderers }
        );

        return files;
    }

    /// <summary>
    /// One atlas bundle, as the Studio's <c>atlasBundle</c> writes it: the bitmaps on one sheet named
    /// after the library, untrimmed - the renderer places each by its own edges - and the asset data
    /// naming each.
    /// </summary>
    private static Dictionary<string, byte[]> Atlas(
        string library,
        List<(string Name, byte[] Png)> bitmaps
    )
    {
        var images = new ImageBundle();

        foreach (var (name, png) in bitmaps)
        {
            images.Images[name] = RgbaImage.Decode(png);
            images.Reference(name);
        }

        var sheet =
            SpriteSheetPacker.Pack(images, library, trim: false)
            ?? throw new FormatException($"The bitmaps of {library} could not be packed.");

        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [$"{library}.json"] = Json(
                new JsonObject
                {
                    ["type"] = library,
                    ["assets"] = new JsonArray([
                        .. images
                            .Referenced.Order(StringComparer.Ordinal)
                            .Select(name => (JsonNode)new JsonObject { ["name"] = name }),
                    ]),
                }
            ),
            [$"{library}_spritesheet.json"] = Json(sheet.Frames),
            [$"{library}.png"] = sheet.Sheet.EncodePng(),
        };
    }

    private static string Uncommented(string? xml) =>
        CommentPattern().Replace(xml ?? string.Empty, "");

    private static byte[] Json(JsonNode node) => JsonSerializer.SerializeToUtf8Bytes(node, JSON);

    [GeneratedRegex(@"<icon\s+name=""([^""]+)""\s+x=""(-?\d+)""\s+y=""(-?\d+)""")]
    private static partial Regex IconPattern();

    [GeneratedRegex(@"<renderer\s+id=""(\d+)""\s+name=""([^""]+)""\s+rendererClass=""([^""]+)""")]
    private static partial Regex RendererPattern();

    [GeneratedRegex("<!--[\\s\\S]*?-->")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"^.*/|\.hab$")]
    private static partial Regex LibraryNamePattern();
}
