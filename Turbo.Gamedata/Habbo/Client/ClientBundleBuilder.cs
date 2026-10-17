using System;
using System.Collections.Generic;
using System.IO;
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
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

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
/// <item><c>habbo-localization-com</c>: the client's embedded texts, a
/// <c>DefaultLocalizations_&lt;lang&gt;.json</c> per language;</item>
/// <item>a window template bundle for each library with a layout, the UI theme in the window
/// manager's (<see cref="ClientTemplates"/>, <see cref="Theme.ClientTheme"/>).</item>
/// </list>
/// A bundle is stamped with the library's hash, so a release that leaves a library as it was builds
/// nothing again; one the hotel uploaded itself is left alone. Then the chat styles follow the
/// release (<see cref="IChatStyleService.FollowClientAsync"/>). What the last build read, left out
/// and could not pack is kept beside the releases (<c>.habbo-client/build.json</c>).
/// </summary>
internal sealed partial class ClientBundleBuilder(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    HabboClientService client,
    IChatStyleService chatStyles,
    TimeProvider time,
    ILogger<ClientBundleBuilder> logger
) : IHabboClientBuilds
{
    private const string BUILD_FILE = "build.json";
    public const string AVATAR_LIBRARY = "habbo-avatar-render-lib";
    public const string VISUALIZATION_LIBRARY = "habbo-room-object-visualization-lib";
    public const string VARIABLE_FX_TABLES = "variable-fx-tables.json";
    public const string LOCALIZATION_LIBRARY = "habbo-localization-com";

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
        LOCALIZATION_LIBRARY,
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
    /// Builds every bundle whose library changed in the followed release - with
    /// <paramref name="force"/>, every one - then lets the chat styles follow it; how many bundles
    /// were built. <paramref name="avatarActions"/> is Habbo's <c>HabboAvatarActions.xml</c>, which
    /// the avatar render library's bundle carries as <c>actions.json</c>; without it (Habbo could not
    /// be asked) a bundle built with it before is kept, as the avatars would only stand without one.
    /// </summary>
    public async Task<int> BuildAsync(
        byte[]? avatarActions,
        PlayerId player,
        bool force,
        Action<string> log,
        CancellationToken ct
    )
    {
        var notes = new List<string>();

        void Note(string message)
        {
            notes.Add(message);
            log(message);
        }

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
            (LOCALIZATION_LIBRARY, string.Empty, Localization),
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
                Note($"{library} is the hotel's own upload: left as it is.");

                continue;
            }

            if (
                !release.FileHashes.TryGetValue(path, out var sha256)
                || release.Read(path) is not { } data
            )
            {
                Note($"Habbo's HTML client {release.Version} has no {path}.");

                continue;
            }

            // Half the library's hash, as the templates have it: the revision column holds 64.
            var revision =
                $"client:{BUILD}:{sha256[..Math.Min(sha256.Length, 32)]}{(stamp.Length > 0 ? ":" + stamp : "")}";

            if (!force && row?.Revision == revision && row.Error is null)
                continue;

            if (
                stamp == NO_ACTIONS
                && row?.Source == AssetBundleSource.Built
                && row.Revision?.EndsWith(":" + NO_ACTIONS, StringComparison.Ordinal) == false
            )
            {
                Note($"{library} kept as it is: Habbo's avatar actions could not be read.");

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
                Note(
                    $"{library} could not be built from Habbo's HTML client {release.Version}: {ex.Message}"
                );

                continue;
            }

            await SaveAsync(dbCtx, row, library, files, revision, release.Version, ct)
                .ConfigureAwait(false);
            built++;
            log($"Built {library} from Habbo's HTML client {release.Version}.");
        }

        var templates = await TemplatesAsync(dbCtx, release, force, log, Note, ct)
            .ConfigureAwait(false);

        built += templates?.Built ?? 0;

        try
        {
            if (await chatStyles.FollowClientAsync(player, ct).ConfigureAwait(false) is { } taken)
                log(
                    $"Chat styles taken from Habbo's HTML client {release.Version}: {taken.Added} added, {taken.Updated} updated, {taken.Kept} kept as the hotel changed them."
                );
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Note($"The chat styles could not be taken from the client: {ex.Message}");
        }

        if (built > 0 || force)
            await WriteBuildAsync(release.Version, templates, notes, ct).ConfigureAwait(false);

        return built;
    }

    public async Task<HabboClientBuildSnapshot> GetAsync(CancellationToken ct)
    {
        var last = await ReadBuildAsync(ct).ConfigureAwait(false);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);
        var rows = await dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == AssetBundleKind.Template)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var names = new HashSet<string>(OWN_BUILDS, StringComparer.Ordinal);

        names.UnionWith(last?.Templates ?? []);
        names.UnionWith(
            rows.Where(x =>
                    x.Source == AssetBundleSource.Built
                    && x.Revision?.StartsWith(TEMPLATES_PREFIX, StringComparison.Ordinal) == true
                )
                .Select(x => x.Name)
        );

        return new HabboClientBuildSnapshot
        {
            Libraries =
            [
                .. rows.Where(x => names.Contains(x.Name))
                    .OrderBy(x => KindOf(x.Name) == "template")
                    .ThenBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => new HabboClientBuiltLibrary
                    {
                        Library = x.Name,
                        Kind = KindOf(x.Name),
                        Uploaded = x.Source == AssetBundleSource.Upload,
                        Size = x.Size,
                        UpdatedAt = x.UpdatedAt,
                    }),
            ],
            Version = last?.Version,
            BuiltAt = last?.BuiltAt,
            Problems = [.. last?.Problems ?? []],
            Notes = [.. last?.Notes ?? []],
        };
    }

    private static string KindOf(string library) =>
        library switch
        {
            AVATAR_LIBRARY => "avatar",
            VISUALIZATION_LIBRARY => "variable-fx",
            LOCALIZATION_LIBRARY => "texts",
            ChatStyles.ChatStyleService.LIBRARY => "chat",
            ClientTemplates.WINDOW_MANAGER => "window-manager",
            _ => "template",
        };

    private string BuildPath => Path.Combine(store.Root, ".habbo-client", BUILD_FILE);

    private async Task<BuildFile?> ReadBuildAsync(CancellationToken ct)
    {
        try
        {
            return File.Exists(BuildPath)
                ? JsonSerializer.Deserialize<BuildFile>(
                    await File.ReadAllTextAsync(BuildPath, ct).ConfigureAwait(false)
                )
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The last build kept: the release, when, what it left out, and the templates' problems - the
    /// ones of the build before when the templates were up to date and not packed again.
    /// </summary>
    private async Task WriteBuildAsync(
        string version,
        TemplatesBuilt? templates,
        List<string> notes,
        CancellationToken ct
    )
    {
        var before = templates is null ? await ReadBuildAsync(ct).ConfigureAwait(false) : null;
        var file = new BuildFile
        {
            Version = version,
            BuiltAt = time.GetUtcNow().UtcDateTime,
            Templates = templates?.Libraries ?? before?.Templates ?? [],
            Problems = templates?.Problems ?? before?.Problems ?? [],
            Notes = notes,
        };
        var temp = $"{BuildPath}.{Guid.NewGuid():N}.part";

        Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)!);
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(file), ct)
            .ConfigureAwait(false);
        File.Move(temp, BuildPath, overwrite: true);
    }

    private sealed record BuildFile
    {
        public string? Version { get; init; }
        public DateTime? BuiltAt { get; init; }
        public List<string> Templates { get; init; } = [];
        public List<string> Problems { get; init; } = [];
        public List<string> Notes { get; init; } = [];
    }

    /// <summary>The templates packed: how many bundles were written, every library packed, and the problems.</summary>
    private sealed record TemplatesBuilt(int Built, List<string> Libraries, List<string> Problems);

    /// <summary>
    /// The window template bundles (<see cref="ClientTemplates"/>): one for each library of the
    /// release with a <c>&lt;layout&gt;</c>, and the window manager's, which every one draws from. All
    /// are stamped with the release's libraries, so they are built again, together, when one changes;
    /// a bundle of a library the release no longer has is taken away, and an uploaded one left alone.
    /// </summary>
    private async Task<TemplatesBuilt?> TemplatesAsync(
        TurboDbContext dbCtx,
        HabboClientRelease release,
        bool force,
        Action<string> log,
        Action<string> note,
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

        if (!force && ours.Count > 0 && ours.All(x => x.Revision == revision && x.Error is null))
            return null;

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
                note($"{path} could not be read: {ex.Message}");

                continue;
            }

            if (name == ClientTemplates.WINDOW_MANAGER)
                windowManager = hab;

            if (name == ClientTemplates.WINDOW_MANAGER || ClientTemplates.HasLayouts(hab))
                libraries.Add((name, hab));
        }

        if (windowManager is null)
        {
            note(
                $"Habbo's HTML client {release.Version} has no window manager library: the templates are not built."
            );

            return new TemplatesBuilt(0, [], []);
        }

        var packed = ClientTemplates.Pack(release.Version, libraries, windowManager);
        var built = 0;

        if (
            packed.Bundles.GetValueOrDefault(ClientTemplates.WINDOW_MANAGER) is
            { } windowManagerFiles
        )
            WithTheme(windowManagerFiles, windowManager, note);

        foreach (var (library, files) in packed.Bundles)
        {
            var row = rows.GetValueOrDefault(library);

            if (row?.Source == AssetBundleSource.Upload)
            {
                note($"{library} is the hotel's own upload: left as it is.");

                continue;
            }

            if (!store.IsValidName(library))
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

        return new TemplatesBuilt(
            built,
            [.. packed.Bundles.Keys.Order(StringComparer.Ordinal)],
            packed.Problems
        );
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

    /// <summary>
    /// The localization library's bundle: the client's embedded texts as nitro-studio reads them
    /// (<c>clientLocalizations</c>) - <c>default_localizations</c> with each language's
    /// <c>default_localizations_&lt;lang&gt;</c> over it - one <c>DefaultLocalizations_&lt;lang&gt;.json</c>
    /// per language. <c>development_localizations</c> is there too, but Flash never loads it.
    /// </summary>
    internal static Dictionary<string, byte[]> Localization(HabLibrary hab)
    {
        var texts = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var entry in hab.Entries)
        {
            var match = LocalizationEntryPattern().Match(entry.Name);

            if (match.Success)
                texts[match.Groups[1].Value] = LocalizationTexts(
                    Encoding.UTF8.GetString(hab.Get(entry))
                );
        }

        if (!texts.TryGetValue(string.Empty, out var @base))
            throw new FormatException("The localization library has no default_localizations.");

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [$"{LOCALIZATION_LIBRARY}.json"] = Json(
                new JsonObject { ["type"] = LOCALIZATION_LIBRARY, ["assets"] = new JsonArray() }
            ),
        };

        foreach (
            var (language, own) in texts
                .Where(x => x.Key.Length > 0)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
        )
        {
            var merged = new JsonObject();

            foreach (var (key, value) in @base)
                merged[key] = value;

            foreach (var (key, value) in own)
                merged[key] = value;

            files[$"DefaultLocalizations_{language}.json"] = Json(merged);
        }

        return files;
    }

    /// <summary>
    /// Flash's <c>CoreLocalizationManager.parseLocalizationData</c>: one <c>key=value</c> a line, a
    /// <c>#</c> line a comment, the key ending at the first <c>=</c>, both sides trimmed, a literal
    /// <c>\n</c> a line break, and an empty value not kept.
    /// </summary>
    internal static Dictionary<string, string> LocalizationTexts(string data)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in LineSeparatorPattern().Split(data))
        {
            if (line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');

            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..]
                .Trim()
                .Replace("\\n", "\n", StringComparison.Ordinal);

            if (key.Length > 0 && value.Length > 0)
                texts[key] = value;
        }

        return texts;
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

    [GeneratedRegex("^default_localizations(?:_([a-z]{2}))?$")]
    private static partial Regex LocalizationEntryPattern();

    [GeneratedRegex(@"\n\r+|\n+|\r+")]
    private static partial Regex LineSeparatorPattern();

    [GeneratedRegex(@"^.*/|\.hab$")]
    private static partial Regex LibraryNamePattern();
}
