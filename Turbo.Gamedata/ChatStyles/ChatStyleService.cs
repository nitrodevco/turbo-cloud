using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Gamedata.ChatStyles;

/// <summary>
/// <see cref="IChatStyleService"/> over <c>player_chat_styles</c>, its bitmaps and the library's
/// other bitmaps. After every change the habbo-free-flow-chat-com bundle is built again as
/// nitro-studio builds it - each style's bitmaps (and the library's others) on one sheet, untrimmed
/// so the reg points still count from each bitmap's edges, and chat-style-definitions.json in the
/// order the client's picker lists them - and kept in the bundle store as a template, where the
/// publish targets take it from.
/// </summary>
internal sealed partial class ChatStyleService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    IChatStyleProvider chatStyles,
    HabboGamedataClient download,
    TimeProvider time,
    ILogger<ChatStyleService> logger
) : IChatStyleService
{
    /// <summary>The library the client loads its chat bubbles from (<c>asset.bundles.templates</c>).</summary>
    public const string LIBRARY = "habbo-free-flow-chat-com";

    public const string DEFINITIONS_FILE = "chat-style-definitions.json";

    /// <summary>The bitmaps every style the client draws has.</summary>
    public static readonly ImmutableArray<string> REQUIRED_BITMAPS =
    [
        "chat_bubble_base",
        "chat_bubble_pointer",
        "selector_preview",
    ];

    /// <summary>The bitmaps only some styles have, which the definitions list by name.</summary>
    public static readonly ImmutableArray<string> OPTIONAL_BITMAPS =
    [
        "chat_bubble_color",
        "icon",
        "chat_bubble_emblem",
        "chat_bubble_emblem_multiline",
    ];

    /// <summary>The longest side a style's bitmap may have; the client's are a few dozen pixels.</summary>
    public const int BITMAP_MAX_SIDE = 512;

    /// <summary>Static styles (the picker's) are numbered under this; 1000-9999 are NFT styles.</summary>
    public const int STATIC_ID_LIMIT = 1000;

    private const int DEFAULT_STYLE_ID = 0;
    private const string STYLE_PREFIX = LIBRARY + "-style_";

    private static readonly JsonSerializerOptions JSON = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SemaphoreSlim _writes = new(1, 1);

    public async Task<ChatStyleList> ListAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .PlayerChatStyles.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ClientStyleId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var bitmaps = (
            await dbCtx
                .PlayerChatStyleBitmaps.AsNoTracking()
                .Select(x => new { x.ChatStyleId, x.File })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToLookup(x => x.ChatStyleId, x => x.File);
        var owners = await dbCtx
            .PlayerOwnedChatStyles.AsNoTracking()
            .GroupBy(x => x.ChatStyleId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);
        var preferring = await dbCtx
            .PlayerSettings.AsNoTracking()
            .Where(x => x.ChatStyleId != DEFAULT_STYLE_ID)
            .GroupBy(x => x.ChatStyleId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);
        var bundle = await dbCtx
            .AssetBundles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Kind == AssetBundleKind.Template && x.Name == LIBRARY, ct)
            .ConfigureAwait(false);
        var library = await dbCtx
            .ChatStyleLibraryBitmaps.AsNoTracking()
            .CountAsync(ct)
            .ConfigureAwait(false);
        var taken = rows.Select(x => x.ClientStyleId).ToHashSet();

        return new ChatStyleList
        {
            Styles =
            [
                .. rows.Select(x =>
                {
                    var files = bitmaps[x.Id].Order(StringComparer.Ordinal).ToImmutableArray();

                    return new ChatStyleDefinitionSnapshot
                    {
                        Id = x.Id,
                        ClientStyleId = x.ClientStyleId,
                        AssetId = x.Name ?? string.Empty,
                        ClubOnly = x.ClubOnly,
                        AmbassadorOnly = x.AmbassadorOnly,
                        StaffOnly = x.StaffOnly,
                        Purchasable = x.Purchasable,
                        System = x.System,
                        StaffOverrideable = x.StaffOverrideable,
                        AllowHtml = x.AllowHtml,
                        Notification = x.Notification,
                        RegPoints = x.RegPoints,
                        Bitmaps = files,
                        SortOrder = x.SortOrder,
                        Owners = owners.GetValueOrDefault(x.Id),
                        Preferring = preferring.GetValueOrDefault(x.ClientStyleId),
                        Drawable = IsDrawable(x, files),
                    };
                }),
            ],
            NextId = Enumerable
                .Range(1, STATIC_ID_LIMIT - 1)
                .FirstOrDefault(x => !taken.Contains(x)),
            Bundle = bundle?.ToSnapshot(true),
            LibraryBitmaps = library,
        };
    }

    public async Task<byte[]?> GetBitmapAsync(int id, string file, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .PlayerChatStyleBitmaps.AsNoTracking()
            .Where(x => x.ChatStyleId == id && x.File == file)
            .Select(x => x.Data)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(
        int id,
        ChatStyleEdit edit,
        PlayerId player,
        CancellationToken ct
    )
    {
        // Everything that can be checked without the database is, before anything is read.
        var assetId = edit.AssetId?.Trim();

        if (assetId is not null && !AssetIdPattern().IsMatch(assetId))
            throw new ArgumentException(
                "A chat style's name is the client's asset id for it: letters, digits, _ and -, 64 at most."
            );

        var regPoints = edit.RegPoints is null ? null : NormalizeRegPoints(edit.RegPoints);
        var files = (edit.Files ?? new Dictionary<string, byte[]?>()).ToDictionary(
            x => x.Key,
            x => x.Value is null ? null : NormalizeBitmap(x.Key, x.Value)
        );

        await _writes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            PlayerChatStyleEntity row;

            if (id == 0)
            {
                row = new PlayerChatStyleEntity
                {
                    ClientStyleId = 0,
                    SortOrder =
                        (
                            await dbCtx
                                .PlayerChatStyles.MaxAsync(x => (int?)x.SortOrder, ct)
                                .ConfigureAwait(false)
                            ?? -1
                        ) + 1,
                    Bitmaps = [],
                };
                dbCtx.PlayerChatStyles.Add(row);
            }
            else
            {
                row =
                    await dbCtx
                        .PlayerChatStyles.Include(x => x.Bitmaps)
                        .FirstOrDefaultAsync(x => x.Id == id, ct)
                        .ConfigureAwait(false)
                    ?? throw new ArgumentException($"There is no chat style {id}.");
            }

            var clientStyleId = edit.ClientStyleId ?? (id == 0 ? null : row.ClientStyleId);

            if (clientStyleId is not >= 0)
                throw new ArgumentException(
                    "A chat style needs the id the client knows its bubble by, from 0."
                );

            assetId ??= row.Name;

            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException(
                    "A chat style needs its asset id: its bitmaps are named after it."
                );

            var others = dbCtx.PlayerChatStyles.AsNoTracking().Where(x => x.Id != id);

            if (
                await others
                    .AnyAsync(x => x.ClientStyleId == clientStyleId, ct)
                    .ConfigureAwait(false)
            )
                throw new ArgumentException($"Chat style {clientStyleId} is in the list already.");

            if (await others.AnyAsync(x => x.Name == assetId, ct).ConfigureAwait(false))
                throw new ArgumentException($"Another chat style is called {assetId}.");

            row.ClientStyleId = clientStyleId.Value;
            row.Name = assetId;
            row.ClubOnly = edit.ClubOnly ?? row.ClubOnly;
            row.AmbassadorOnly = edit.AmbassadorOnly ?? row.AmbassadorOnly;
            row.StaffOnly = edit.StaffOnly ?? row.StaffOnly;
            row.Purchasable = edit.Purchasable ?? row.Purchasable;
            row.System = edit.System ?? row.System;
            row.StaffOverrideable = edit.StaffOverrideable ?? row.StaffOverrideable;
            row.AllowHtml = edit.AllowHtml ?? row.AllowHtml;
            row.Notification = edit.Notification ?? row.Notification;
            row.RegPoints = regPoints ?? row.RegPoints;

            if (
                row.ClientStyleId == DEFAULT_STYLE_ID
                && (
                    row.ClubOnly
                    || row.AmbassadorOnly
                    || row.StaffOnly
                    || row.Purchasable
                    || row.System
                )
            )
                throw new ArgumentException(
                    "The normal bubble (0) is everyone's: a line in a bubble its speaker may not use falls back to it."
                );

            var bitmaps = row.Bitmaps ??= [];

            // A new style starts with the bitmaps of the one it was made from.
            if (id == 0 && edit.CopyFrom is { } from)
            {
                var source = await dbCtx
                    .PlayerChatStyleBitmaps.AsNoTracking()
                    .Where(x => x.ChatStyleId == from)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var bitmap in source.Where(x => !files.ContainsKey(x.File)))
                    bitmaps.Add(
                        new PlayerChatStyleBitmapEntity
                        {
                            ChatStyleId = 0,
                            File = bitmap.File,
                            Data = bitmap.Data,
                        }
                    );
            }

            foreach (var (file, data) in files)
            {
                var kept = bitmaps.FirstOrDefault(x => x.File == file);

                if (data is null)
                {
                    if (kept is not null)
                        bitmaps.Remove(kept);
                }
                else if (kept is not null)
                    kept.Data = data;
                else
                    bitmaps.Add(
                        new PlayerChatStyleBitmapEntity
                        {
                            ChatStyleId = 0,
                            File = file,
                            Data = data,
                        }
                    );
            }

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Player {PlayerId} saved chat style {ClientStyleId} ({AssetId}) with {Bitmaps} bitmaps",
                player,
                row.ClientStyleId,
                row.Name,
                bitmaps.Count
            );

            await AfterChangeAsync(dbCtx, ct).ConfigureAwait(false);

            return row.Id;
        }
        finally
        {
            _writes.Release();
        }
    }

    public async Task<bool> DeleteAsync(int id, PlayerId player, CancellationToken ct)
    {
        await _writes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var row = await dbCtx
                .PlayerChatStyles.FirstOrDefaultAsync(x => x.Id == id, ct)
                .ConfigureAwait(false);

            if (row is null)
                return false;

            if (row.ClientStyleId == DEFAULT_STYLE_ID)
                throw new ArgumentException(
                    "The normal bubble (0) can't be removed: lines fall back to it."
                );

            var owners = await dbCtx
                .PlayerOwnedChatStyles.CountAsync(x => x.ChatStyleId == id, ct)
                .ConfigureAwait(false);

            if (owners > 0)
                throw new ArgumentException(
                    $"{owners} {(owners == 1 ? "player owns" : "players own")} it; make it a system style instead to stop it being used."
                );

            dbCtx.PlayerChatStyles.Remove(row);
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Player {PlayerId} removed chat style {ClientStyleId} ({AssetId})",
                player,
                row.ClientStyleId,
                row.Name
            );

            await AfterChangeAsync(dbCtx, ct).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _writes.Release();
        }
    }

    public async Task<ChatStyleImportResult> ImportBundleFromAsync(
        string url,
        PlayerId player,
        CancellationToken ct
    )
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
        )
            throw new ArgumentException($"{url} is not a web address.");

        var bundle =
            await download.GetFileAsync(address.AbsoluteUri, ct).ConfigureAwait(false)
            ?? throw new ArgumentException($"There is no bundle at {address.AbsoluteUri}.");

        return await ImportBundleAsync(bundle, player, ct).ConfigureAwait(false);
    }

    public async Task<ChatStyleImportResult> ImportBundleAsync(
        byte[] bundle,
        PlayerId player,
        CancellationToken ct
    )
    {
        var read = ReadBundle(bundle);

        await _writes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);
            var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var txScope = tx.ConfigureAwait(false);

            var rows = (
                await dbCtx
                    .PlayerChatStyles.Include(x => x.Bitmaps)
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            ).ToDictionary(x => x.ClientStyleId);
            var added = 0;
            var updated = 0;

            foreach (var (style, order) in read.Styles.Select((x, i) => (x, i)))
            {
                if (!rows.TryGetValue(style.Id, out var row))
                {
                    // A style the hotel hasn't had: who may speak with it is what the client says.
                    row = new PlayerChatStyleEntity
                    {
                        ClientStyleId = style.Id,
                        ClubOnly = style.Flag("hcOnly"),
                        AmbassadorOnly = style.Flag("ambassadorOnly"),
                        Purchasable =
                            style.Flag("purchasable") || style.Id is >= STATIC_ID_LIMIT and < 10000,
                        System = style.Flag("systemStyle"),
                        StaffOnly = style.AssetId == "staff",
                        Bitmaps = [],
                    };
                    dbCtx.PlayerChatStyles.Add(row);
                    rows[style.Id] = row;
                    added++;
                }
                else
                    updated++;

                // Another row with this asset id gives it up: the bundle's is the look it names.
                foreach (var other in rows.Values.Where(x => x != row && x.Name == style.AssetId))
                    other.Name = null;

                row.Name = style.AssetId;
                row.StaffOverrideable = style.Flag("staffOverrideable");
                row.AllowHtml = style.Flag("allowHTML");
                row.Notification = style.Flag("notification");
                row.RegPoints = style.RegPoints;
                row.SortOrder = order;

                var bitmaps = row.Bitmaps ??= [];

                bitmaps.Clear();

                foreach (var (file, data) in style.Bitmaps)
                    bitmaps.Add(
                        new PlayerChatStyleBitmapEntity
                        {
                            ChatStyleId = row.Id,
                            File = file,
                            Data = data,
                        }
                    );
            }

            await dbCtx.ChatStyleLibraryBitmaps.ExecuteDeleteAsync(ct).ConfigureAwait(false);

            foreach (var (name, data) in read.Library)
                dbCtx.ChatStyleLibraryBitmaps.Add(
                    new ChatStyleLibraryBitmapEntity { Name = name, Data = data }
                );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Player {PlayerId} imported a chat style bundle: {Added} styles added, {Updated} updated, {Library} library bitmaps",
                player,
                added,
                updated,
                read.Library.Count
            );

            await AfterChangeAsync(dbCtx, ct).ConfigureAwait(false);

            return new ChatStyleImportResult
            {
                Added = added,
                Updated = updated,
                LibraryBitmaps = read.Library.Count,
            };
        }
        finally
        {
            _writes.Release();
        }
    }

    /// <summary>What a chat changes: who may speak with a style at once, and the bundle the client draws them from.</summary>
    private async Task AfterChangeAsync(TurboDbContext dbCtx, CancellationToken ct)
    {
        await chatStyles.ReloadAsync(ct).ConfigureAwait(false);
        await BuildAsync(dbCtx, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The habbo-free-flow-chat-com bundle, built from every style the client can draw and the
    /// library's other bitmaps, and kept in the bundle store; nothing when no style can be drawn.
    /// </summary>
    private async Task BuildAsync(TurboDbContext dbCtx, CancellationToken ct)
    {
        var rows = await dbCtx
            .PlayerChatStyles.AsNoTracking()
            .Include(x => x.Bitmaps)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ClientStyleId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var library = await dbCtx
            .ChatStyleLibraryBitmaps.AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var drawn = rows.Where(x => IsDrawable(x, [.. (x.Bitmaps ?? []).Select(b => b.File)]))
            .ToList();

        if (drawn.Count == 0)
            return;

        var images = new ImageBundle();

        void Add(string name, byte[] data)
        {
            images.Images[name] = RgbaImage.Decode(data);
            images.Reference(name);
        }

        foreach (var row in drawn)
        foreach (var bitmap in row.Bitmaps!)
            Add($"{STYLE_PREFIX}{row.Name}_{bitmap.File}", bitmap.Data);

        foreach (var bitmap in library)
            Add(bitmap.Name, bitmap.Data);

        var sheet =
            SpriteSheetPacker.Pack(images, LIBRARY, trim: false)
            ?? throw new InvalidOperationException("The chat styles' bitmaps could not be packed.");
        var definitions = new JsonObject
        {
            ["defaultId"] = DEFAULT_STYLE_ID,
            ["styles"] = new JsonArray([.. drawn.Select(row => (JsonNode)Definition(row))]),
        };
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [$"{LIBRARY}.json"] = Json(
                new JsonObject
                {
                    ["type"] = LIBRARY,
                    ["assets"] = new JsonArray([
                        .. images
                            .Referenced.Order(StringComparer.Ordinal)
                            .Select(name => (JsonNode)new JsonObject { ["name"] = name }),
                    ]),
                }
            ),
            [$"{LIBRARY}_spritesheet.json"] = Json(sheet.Frames),
            [$"{LIBRARY}.png"] = sheet.Sheet.EncodePng(),
            [DEFINITIONS_FILE] = Json(definitions),
        };
        var data = NitroBundle.Create(files).Write();
        var hash = await store
            .WriteAsync(AssetBundleKind.Template, LIBRARY, data, ct)
            .ConfigureAwait(false);
        var bundle = await dbCtx
            .AssetBundles.FirstOrDefaultAsync(
                x => x.Kind == AssetBundleKind.Template && x.Name == LIBRARY,
                ct
            )
            .ConfigureAwait(false);

        if (bundle is null)
        {
            bundle = new AssetBundleEntity
            {
                Kind = AssetBundleKind.Template,
                Name = LIBRARY,
                Source = AssetBundleSource.Built,
            };
            dbCtx.AssetBundles.Add(bundle);
        }

        bundle.Source = AssetBundleSource.Built;
        bundle.Revision = null;
        bundle.Hash = hash;
        bundle.Size = data.LongLength;
        bundle.Error = null;
        bundle.Retry = false;
        bundle.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Built the {Library} bundle: {Styles} chat styles, {Bytes} bytes ({Hash})",
            LIBRARY,
            drawn.Count,
            data.LongLength,
            hash
        );
    }

    /// <summary>A style's entry of chat-style-definitions.json: only the flags that are on, as the client's file has them.</summary>
    private static JsonObject Definition(PlayerChatStyleEntity row)
    {
        var flags = new JsonObject();

        void Flag(string name, bool on)
        {
            if (on)
                flags[name] = true;
        }

        Flag("systemStyle", row.System);
        Flag("purchasable", row.Purchasable);
        Flag("hcOnly", row.ClubOnly);
        Flag("staffOverrideable", row.StaffOverrideable);
        Flag("allowHTML", row.AllowHtml);
        Flag("ambassadorOnly", row.AmbassadorOnly);
        Flag("notification", row.Notification);

        var files = (row.Bitmaps ?? []).Select(x => x.File).ToHashSet(StringComparer.Ordinal);

        return new JsonObject
        {
            ["id"] = row.ClientStyleId,
            ["assetId"] = row.Name,
            ["flags"] = flags,
            ["regPoints"] = JsonNode.Parse(row.RegPoints!),
            ["bitmaps"] = new JsonArray([
                .. OPTIONAL_BITMAPS.Where(files.Contains).Select(x => (JsonNode)x),
            ]),
        };
    }

    private static bool IsDrawable(PlayerChatStyleEntity row, IReadOnlyCollection<string> files) =>
        !string.IsNullOrEmpty(row.Name)
        && row.RegPoints is not null
        && REQUIRED_BITMAPS.All(files.Contains);

    /// <summary>A bitmap as the bundle keeps it: an image of a style's own files, read and written again as PNG.</summary>
    private static byte[] NormalizeBitmap(string file, byte[] data)
    {
        if (!REQUIRED_BITMAPS.Contains(file) && !OPTIONAL_BITMAPS.Contains(file))
            throw new ArgumentException(
                $"{file} is not a chat style's bitmap: {string.Join(", ", REQUIRED_BITMAPS.Concat(OPTIONAL_BITMAPS))}."
            );

        RgbaImage image;

        try
        {
            image = RgbaImage.Decode(data);
        }
        catch (AssetFormatException ex)
        {
            throw new ArgumentException($"{file} is not an image: {ex.Message}", ex);
        }

        if (image.Width > BITMAP_MAX_SIDE || image.Height > BITMAP_MAX_SIDE)
            throw new ArgumentException(
                $"{file} is {image.Width}x{image.Height}; a chat style's bitmap is {BITMAP_MAX_SIDE} pixels a side at most."
            );

        return image.EncodePng();
    }

    /// <summary>
    /// Reg points as the client reads them (<c>ChatStyleRegPoints</c>), written compactly: the
    /// nine-slice and the text's margins, and any of the others by their kind. A key the client
    /// doesn't read, or a value of the wrong kind, is refused.
    /// </summary>
    internal static string NormalizeRegPoints(string json)
    {
        JsonObject points;

        try
        {
            points =
                JsonNode.Parse(json) as JsonObject
                ?? throw new ArgumentException("Reg points are a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The reg points are not JSON: {ex.Message}", ex);
        }

        var normalized = new JsonObject();

        foreach (var (key, value) in points)
        {
            if (value is null)
                continue;

            normalized[key] = RegPointKinds.TryGetValue(key, out var kind)
                ? kind switch
                {
                    RegPointKind.Rect => Rect(key, value),
                    RegPointKind.Point => Point(key, value),
                    RegPointKind.Int => Int(key, value),
                    RegPointKind.Ints => new JsonArray([
                        .. (value as JsonArray ?? throw Wrong(key, "a list of numbers")).Select(x =>
                            (JsonNode)Int(key, x!)
                        ),
                    ]),
                    RegPointKind.Bool => value is JsonValue flag
                    && flag.TryGetValue<bool>(out var on)
                        ? JsonValue.Create(on)
                        : throw Wrong(key, "true or false"),
                    _ => value is JsonValue text
                    && text.TryGetValue<string>(out var words)
                    && words.Length <= 64
                        ? JsonValue.Create(words)
                        : throw Wrong(key, "a text of 64 at most"),
                }
                : throw new ArgumentException($"{key} is not a reg point the client reads.");
        }

        foreach (var required in (string[])["scale9", "textFieldMargins"])
            if (!normalized.ContainsKey(required))
                throw new ArgumentException($"A chat style's reg points need {required}.");

        return normalized.ToJsonString(JSON);
    }

    private static JsonObject Rect(string key, JsonNode value) =>
        value is JsonObject rect
            ? new JsonObject
            {
                ["x"] = Int(key, rect["x"]),
                ["y"] = Int(key, rect["y"]),
                ["width"] = Int(key, rect["width"]),
                ["height"] = Int(key, rect["height"]),
            }
            : throw Wrong(key, "a rectangle (x, y, width, height)");

    private static JsonObject Point(string key, JsonNode value) =>
        value is JsonObject point
            ? new JsonObject { ["x"] = Int(key, point["x"]), ["y"] = Int(key, point["y"]) }
            : throw Wrong(key, "a point (x, y)");

    private static JsonValue Int(string key, JsonNode? value) =>
        value is JsonValue number && number.TryGetValue<double>(out var x) && x == Math.Floor(x)
            ? JsonValue.Create((long)x)
            : throw Wrong(key, "whole numbers");

    private static ArgumentException Wrong(string key, string what) =>
        new($"{key} must be {what}.");

    private enum RegPointKind
    {
        Rect,
        Point,
        Int,
        Ints,
        Bool,
        Text,
    }

    private static readonly Dictionary<string, RegPointKind> RegPointKinds = new(
        StringComparer.Ordinal
    )
    {
        ["scale9"] = RegPointKind.Rect,
        ["textFieldMargins"] = RegPointKind.Rect,
        ["overlapRect"] = RegPointKind.Rect,
        ["faceXY"] = RegPointKind.Point,
        ["colorXY"] = RegPointKind.Point,
        ["emblemXY"] = RegPointKind.Point,
        ["emblemMultilineXY"] = RegPointKind.Point,
        ["pointerY"] = RegPointKind.Int,
        ["textColorRGB"] = RegPointKind.Int,
        ["fontSize"] = RegPointKind.Int,
        ["linkColorRGB"] = RegPointKind.Int,
        ["linkHoverColorRGB"] = RegPointKind.Int,
        ["linkActiveColorRGB"] = RegPointKind.Int,
        ["pointerXMargins"] = RegPointKind.Ints,
        ["anonymous"] = RegPointKind.Bool,
        ["usePixelPerfectNineSlice"] = RegPointKind.Bool,
        ["fontFace"] = RegPointKind.Text,
    };

    /// <summary>
    /// A habbo-free-flow-chat-com bundle read back: each style its definitions name, with its reg
    /// points and its bitmaps cut from the sheet (put back to their own size where the packer
    /// trimmed them), and the library's other bitmaps.
    /// </summary>
    private static ReadBundleResult ReadBundle(byte[] data)
    {
        NitroBundle bundle;

        try
        {
            bundle = NitroBundle.Read(data);
        }
        catch (AssetFormatException ex)
        {
            throw new ArgumentException($"The file is not a .nitro bundle: {ex.Message}", ex);
        }

        JsonNode? Read(string name) =>
            bundle.Files.TryGetValue(name, out var file) ? JsonNode.Parse(file) : null;

        var definitions =
            Read(DEFINITIONS_FILE)?["styles"] as JsonArray
            ?? throw new ArgumentException(
                $"The bundle has no {DEFINITIONS_FILE}: it is not the chat styles' {LIBRARY}."
            );
        var sheetName = bundle.Files.Keys.FirstOrDefault(x =>
            x.EndsWith("_spritesheet.json", StringComparison.Ordinal)
        );
        var frames =
            (sheetName is null ? null : Read(sheetName)?["frames"] as JsonObject)
            ?? throw new ArgumentException("The bundle has no spritesheet.");
        var imageName =
            Read(sheetName!)?["meta"]?["image"]?.GetValue<string>()
            ?? sheetName![..^"_spritesheet.json".Length] + ".png";
        var sheet = bundle.Files.TryGetValue(imageName, out var png)
            ? RgbaImage.Decode(png)
            : throw new ArgumentException($"The bundle has no {imageName}.");
        var cut = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var (name, node) in frames)
        {
            if (node is not JsonObject frame || frame["rotated"]?.GetValue<bool>() == true)
                continue;

            var rect = frame["frame"]!;
            var source = frame["spriteSourceSize"];
            var size = frame["sourceSize"];
            var w = rect["w"]!.GetValue<int>();
            var h = rect["h"]!.GetValue<int>();

            cut[name] = sheet
                .Crop(
                    rect["x"]!.GetValue<int>(),
                    rect["y"]!.GetValue<int>(),
                    w,
                    h,
                    size?["w"]?.GetValue<int>() ?? w,
                    size?["h"]?.GetValue<int>() ?? h,
                    source?["x"]?.GetValue<int>() ?? 0,
                    source?["y"]?.GetValue<int>() ?? 0
                )
                .EncodePng();
        }

        var styles = new List<ImportedStyle>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in definitions.OfType<JsonObject>())
        {
            var id = node["id"]?.GetValue<int>();
            var assetId = node["assetId"]?.GetValue<string>();

            if (id is not >= 0 || assetId is null || !AssetIdPattern().IsMatch(assetId))
                continue;

            var bitmaps = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var file in REQUIRED_BITMAPS.Concat(OPTIONAL_BITMAPS))
            {
                var name = $"{STYLE_PREFIX}{assetId}_{file}";

                if (cut.TryGetValue(name, out var bitmap))
                {
                    bitmaps[file] = bitmap;
                    claimed.Add(name);
                }
            }

            styles.Add(
                new ImportedStyle(
                    id.Value,
                    assetId,
                    node["flags"] as JsonObject ?? [],
                    NormalizeRegPoints(node["regPoints"]?.ToJsonString() ?? "{}"),
                    bitmaps
                )
            );
        }

        return new ReadBundleResult(
            styles,
            [
                .. cut.Where(x =>
                        !claimed.Contains(x.Key)
                        && !x.Key.StartsWith(STYLE_PREFIX, StringComparison.Ordinal)
                        && x.Key.Length <= ChatStyleLibraryBitmapEntity.NAME_MAX_LENGTH
                    )
                    .Select(x => (x.Key, x.Value)),
            ]
        );
    }

    private static byte[] Json(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString(JSON));

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex AssetIdPattern();

    private sealed record ImportedStyle(
        int Id,
        string AssetId,
        JsonObject Flags,
        string RegPoints,
        Dictionary<string, byte[]> Bitmaps
    )
    {
        public bool Flag(string name) =>
            Flags[name] is JsonValue value && value.TryGetValue<bool>(out var on) && on;
    }

    private sealed record ReadBundleResult(
        List<ImportedStyle> Styles,
        List<(string Name, byte[] Data)> Library
    );
}
