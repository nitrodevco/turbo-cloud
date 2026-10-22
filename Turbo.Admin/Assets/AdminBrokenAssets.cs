using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Assets;

/// <summary>
/// Every asset players can't load, in one list, with the fixes that apply: the addresses players'
/// clients answered 404 for (told by the client errors plugin, read while it is loaded), what the
/// checks of the bundles found, the catalog pictures pages show that the hotel does not have, and
/// the badges players hold with no picture. Staff fix one (take it from Habbo, upload it, publish)
/// or ignore it with a note; an ignored one is listed apart until it is brought back.
/// </summary>
public sealed class AdminBrokenAssets(
    IDbContextFactory<TurboDbContext> database,
    IAssetBundleStore store,
    IAssetBundleService bundles,
    IAssetPublishService publishing,
    AdminCatalogImages catalogImages,
    IPluginAdministration plugins,
    TimeProvider time,
    ILogger<AdminBrokenAssets> logger
)
{
    public const string CLIENT = "client";
    public const string CHECK = "check";
    public const string CATALOG = "catalog";
    public const string BADGE = "badge";

    /// <summary>The client errors plugin's key: its notices tell of the addresses clients could not load.</summary>
    public const string CLIENT_ERRORS_PLUGIN = "clienterrors";

    public const string TAKE_FROM_HABBO = "habbo";
    public const string UPLOAD = "upload";
    public const string PUBLISH = "publish";

    private static readonly string[] Sources = [CLIENT, CHECK, CATALOG, BADGE];

    /// <summary>The kinds a sync from Habbo takes: what "take from Habbo" can fix.</summary>
    private static readonly HashSet<AssetBundleKind> FromHabbo =
    [
        AssetBundleKind.Furniture,
        AssetBundleKind.Effect,
        AssetBundleKind.Pet,
        AssetBundleKind.Figure,
        AssetBundleKind.FurniIcon,
        AssetBundleKind.CatalogIcon,
        AssetBundleKind.Badge,
        AssetBundleKind.CatalogImage,
        AssetBundleKind.Sound,
    ];

    public async Task<BrokenAssetsResponse> ListAsync(
        Func<IEnumerable<int>, Task<IReadOnlyDictionary<int, string>?>> names,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var ignores = await db
            .AssetBrokenIgnores.AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var staff = await names(ignores.Select(x => x.PlayerId)).ConfigureAwait(false);
        var ignored = ignores.ToDictionary(
            x => (x.Source, x.Key),
            x => new BrokenAssetIgnored(
                x.Note,
                x.PlayerId,
                staff?.GetValueOrDefault(x.PlayerId),
                x.CreatedAt
            )
        );

        var clientRead = plugins
            .List()
            .Any(x => x.Key == CLIENT_ERRORS_PLUGIN && x.State == PluginStateType.Loaded);
        var client = clientRead ? await ClientAsync(db, ct).ConfigureAwait(false) : [];
        var checks = await ChecksAsync(ct).ConfigureAwait(false);
        var catalog = await CatalogAsync(ct).ConfigureAwait(false);
        var badges = await BadgesAsync(db, ct).ConfigureAwait(false);

        BrokenAssetItem Mark(BrokenAssetItem item) =>
            ignored.TryGetValue((item.Source, item.Key), out var why)
                ? item with
                {
                    Ignored = why,
                }
                : item;

        BrokenAssetSource Source(string id, string title, List<BrokenAssetItem> items) =>
            new(id, title, true, null, items.Count(x => !ignored.ContainsKey((x.Source, x.Key))));

        return new BrokenAssetsResponse(
            [
                clientRead
                    ? Source(CLIENT, "Players' clients", client)
                    : new BrokenAssetSource(
                        CLIENT,
                        "Players' clients",
                        false,
                        "The client errors plugin is not loaded, so the addresses clients could not load are not known.",
                        0
                    ),
                Source(CHECK, "Checks of the bundles", checks),
                Source(CATALOG, "Catalog pictures", catalog),
                Source(BADGE, "Badges", badges),
            ],
            [.. client.Concat(checks).Concat(catalog).Concat(badges).Select(Mark)]
        );
    }

    /// <summary>
    /// Ignores a broken asset with why, or changes why; false for a source that isn't one or a
    /// key that can't be kept. Throws <see cref="ArgumentException"/> with why for a note too long.
    /// </summary>
    public async Task<bool> IgnoreAsync(
        string source,
        string key,
        string note,
        PlayerId player,
        CancellationToken ct
    )
    {
        if (!IsKey(source, key))
            return false;

        note = note.Trim();

        if (note.Length > AssetBrokenIgnoreEntity.NOTE_MAX_LENGTH)
            throw new ArgumentException(
                $"A note can be at most {AssetBrokenIgnoreEntity.NOTE_MAX_LENGTH} characters."
            );

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .AssetBrokenIgnores.SingleOrDefaultAsync(x => x.Source == source && x.Key == key, ct)
            .ConfigureAwait(false);

        if (row is null)
            db.AssetBrokenIgnores.Add(
                new AssetBrokenIgnoreEntity
                {
                    Source = source,
                    Key = key,
                    Note = note,
                    PlayerId = player.Value,
                }
            );
        else
        {
            row.Note = note;
            row.PlayerId = player.Value;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} ignored the broken asset {Source} {Key}: {Note}",
            player,
            source,
            key,
            note
        );

        return true;
    }

    /// <summary>
    /// Keeps an uploaded GIF or PNG as the picture of the badge <paramref name="code"/>
    /// (<c>images/badges/&lt;code&gt;.gif</c>, the file the client asks for), recorded so a publish
    /// sends it: the one picture the panel had no upload for. Throws <see cref="ArgumentException"/>,
    /// with why, for a code or file it can't take.
    /// </summary>
    public async Task UploadBadgeAsync(
        string code,
        string fileName,
        byte[] data,
        PlayerId player,
        CancellationToken ct
    )
    {
        const AssetBundleKind kind = AssetBundleKind.Badge;
        var name = $"{code.Trim()}.gif";

        if (data.Length is 0 or > AdminCatalogImages.UPLOAD_MAX_BYTES)
            throw new ArgumentException(
                $"A picture is at most {AdminCatalogImages.UPLOAD_MAX_BYTES / (1024 * 1024)} MB, and not empty."
            );

        if (!IsPicture(data))
            throw new ArgumentException($"{fileName} is not a GIF or a PNG.");

        if (!store.IsValidName(name))
            throw new ArgumentException($"\"{code}\" can't be a badge's code.");

        var hash = await store.WriteAsync(kind, name, data, ct).ConfigureAwait(false);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        // What a publish sends: the files recorded with their hash.
        var row = await db
            .AssetBundles.FirstOrDefaultAsync(x => x.Kind == kind && x.Name == name, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AssetBundleEntity
            {
                Kind = kind,
                Name = name,
                Source = AssetBundleSource.Upload,
            };
            db.AssetBundles.Add(row);
        }

        row.Source = AssetBundleSource.Upload;
        row.Revision = null;
        row.Hash = hash;
        row.Size = data.LongLength;
        row.Error = null;
        row.Retry = false;
        row.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} uploaded the picture of the badge {Code} ({Hash})",
            player,
            code,
            hash
        );
    }

    private static bool IsPicture(ReadOnlySpan<byte> data) =>
        data.StartsWith("GIF87a"u8)
        || data.StartsWith("GIF89a"u8)
        || data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    /// <summary>Brings an ignored broken asset back into the list; false when it was not ignored.</summary>
    public async Task<bool> UnignoreAsync(
        string source,
        string key,
        PlayerId player,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var removed = await db
            .AssetBrokenIgnores.Where(x => x.Source == source && x.Key == key)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (removed > 0)
            logger.LogInformation(
                "Player {PlayerId} brought back the broken asset {Source} {Key}",
                player,
                source,
                key
            );

        return removed > 0;
    }

    /// <summary>
    /// Keeps an address a client could not load, as the client errors plugin told of it: a new
    /// one is added, a known one counted again.
    /// </summary>
    public async Task RecordAsync(BrokenAssetReport report, CancellationToken ct)
    {
        if (report.Url.Length > AssetBrokenReportEntity.URL_MAX_LENGTH)
            return;

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var now = time.GetUtcNow().UtcDateTime;
        var issue = report.IssueUrl is { Length: <= AssetBrokenReportEntity.URL_MAX_LENGTH }
            ? report.IssueUrl
            : null;
        var row = await db
            .AssetBrokenReports.SingleOrDefaultAsync(x => x.Url == report.Url, ct)
            .ConfigureAwait(false);

        if (row is null)
            db.AssetBrokenReports.Add(
                new AssetBrokenReportEntity
                {
                    Url = report.Url,
                    Status = report.Status,
                    Reports = 1,
                    IssueUrl = issue,
                    LastSeenAt = now,
                }
            );
        else
        {
            row.Status = report.Status;
            row.Reports++;
            row.IssueUrl = issue ?? row.IssueUrl;
            row.LastSeenAt = now;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static bool IsKey(string source, string key) =>
        Sources.Contains(source) && key.Length is > 0 and <= AssetBrokenIgnoreEntity.KEY_MAX_LENGTH;

    /// <summary>
    /// The addresses clients could not load, each matched to the bundle it is under a publish
    /// target's public URL: one the store has is published (the target, or a cache in front of
    /// it, lacks it); one it does not is taken from Habbo or uploaded.
    /// </summary>
    private async Task<List<BrokenAssetItem>> ClientAsync(TurboDbContext db, CancellationToken ct)
    {
        var reports = await db
            .AssetBrokenReports.AsNoTracking()
            .OrderByDescending(x => x.LastSeenAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (reports.Count == 0)
            return [];

        var roots = (await publishing.ListTargetsAsync(ct).ConfigureAwait(false))
            .Select(x => x.PublicUrl.Trim().TrimEnd('/'))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => x + "/")
            .ToList();
        var files = (await store.ListFilesAsync(ct).ConfigureAwait(false)).ToDictionary(
            x => x.Path,
            StringComparer.Ordinal
        );

        return
        [
            .. reports.Select(report =>
            {
                var times = report.Reports == 1 ? "once" : $"{report.Reports} times";
                var told =
                    $"HTTP {report.Status}, told {times}, last {report.LastSeenAt:yyyy-MM-dd HH:mm} UTC";
                var path = PathUnder(report.Url, roots);

                if (path is null)
                    return Item(
                        CLIENT,
                        report.Url,
                        report.Url,
                        null,
                        null,
                        $"{told}. It is not under a publish target's public URL.",
                        report.Reports,
                        [],
                        report.IssueUrl
                    );

                if (files.TryGetValue(path, out var file))
                    return Item(
                        CLIENT,
                        report.Url,
                        report.Url,
                        file.Kind,
                        file.Name,
                        $"{told}. The hotel has it: publish it (and purge the cache in front of the target).",
                        report.Reports,
                        [PUBLISH],
                        report.IssueUrl
                    );

                var (kind, name) = BundleOf(path);

                return Item(
                    CLIENT,
                    report.Url,
                    report.Url,
                    kind,
                    name,
                    kind is null
                        ? $"{told}. It is no asset the hotel keeps."
                        : $"{told}. The hotel does not have it.",
                    report.Reports,
                    FixesOf(kind),
                    report.IssueUrl
                );
            }),
        ];
    }

    private async Task<List<BrokenAssetItem>> ChecksAsync(CancellationToken ct) =>
        [
            .. (await bundles.GetCheckFindingsAsync(ct).ConfigureAwait(false)).Select(x =>
                Item(
                    CHECK,
                    x.Kind is { } kind && x.Name is { } name
                        ? $"{x.CheckId}:{AssetContractNames.Of(kind)}/{name}"
                        : $"{x.CheckId}:{x.Label}",
                    x.Name ?? x.Label,
                    x.Kind,
                    x.Name,
                    CheckDetail(x),
                    1,
                    FixesOf(x.Kind),
                    null
                )
            ),
        ];

    private async Task<List<BrokenAssetItem>> CatalogAsync(CancellationToken ct) =>
        [
            .. (await catalogImages.MissingAsync(ct).ConfigureAwait(false)).Select(x =>
                Item(
                    CATALOG,
                    x.File,
                    x.Name,
                    AssetBundleKind.CatalogImage,
                    x.File,
                    $"Shown on {string.Join(", ", x.Pages.Select(p => $"\"{p.Title}\" ({p.Id})"))}; the hotel does not have it.",
                    x.Pages.Length,
                    FixesOf(AssetBundleKind.CatalogImage),
                    null
                )
            ),
        ];

    /// <summary>The badges players hold whose picture the hotel does not have, by how many hold each.</summary>
    private async Task<List<BrokenAssetItem>> BadgesAsync(TurboDbContext db, CancellationToken ct)
    {
        var held = await db
            .PlayerBadges.AsNoTracking()
            .GroupBy(x => x.BadgeCode)
            .Select(g => new
            {
                Code = g.Key,
                Players = g.Select(x => x.PlayerEntityId).Distinct().Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var pictures = store.NamesOnDisk(AssetBundleKind.Badge);

        return
        [
            .. held.Select(x => (x.Code, x.Players, File: $"{x.Code}.gif"))
                .Where(x => store.IsValidName(x.File) && !pictures.Contains(x.File))
                .OrderByDescending(x => x.Players)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .Select(x =>
                    Item(
                        BADGE,
                        x.Code,
                        x.Code,
                        AssetBundleKind.Badge,
                        x.File,
                        $"Held by {(x.Players == 1 ? "1 player" : $"{x.Players} players")}; the hotel has no picture for it.",
                        x.Players,
                        FixesOf(AssetBundleKind.Badge),
                        null
                    )
                ),
        ];
    }

    private static string CheckDetail(AssetCheckFinding finding) =>
        finding.CheckId switch
        {
            "furniture-missing" =>
                "A furniture definition names it and no bundle has it: the client draws a placeholder.",
            "file-missing" => "Its row says it has a file that the bundle folder does not have.",
            "failed" => $"It could not be downloaded or converted: {finding.Detail}",
            "effects-missing" => "The catalog sells it and no effect bundle carries it.",
            "pets-missing" => "Its breeds are in the hotel and no pet bundle carries it.",
            _ => finding.Label,
        };

    private static string[] FixesOf(AssetBundleKind? kind) =>
        kind is not { } known ? []
        : FromHabbo.Contains(known) ? [TAKE_FROM_HABBO, UPLOAD]
        : [UPLOAD];

    private static BrokenAssetItem Item(
        string source,
        string key,
        string title,
        AssetBundleKind? kind,
        string? name,
        string detail,
        int count,
        string[] actions,
        string? link
    ) =>
        new(
            source,
            key,
            title,
            kind is { } k ? AssetContractNames.Of(k) : null,
            name,
            detail,
            count,
            actions,
            link,
            null
        );

    /// <summary>The path of <paramref name="url"/> under one of <paramref name="roots"/>, without its query; null under none.</summary>
    private static string? PathUnder(string url, IEnumerable<string> roots)
    {
        var bare = url.Split('?', '#')[0];

        foreach (var root in roots)
        {
            if (bare.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(bare[root.Length..]);
        }

        return null;
    }

    /// <summary>The kind and name of the bundle kept at <paramref name="path"/>, when it is one's place.</summary>
    private (AssetBundleKind? Kind, string? Name) BundleOf(string path)
    {
        var slash = path.LastIndexOf('/');
        var file = path[(slash + 1)..];
        var dot = file.LastIndexOf('.');

        foreach (var kind in Enum.GetValues<AssetBundleKind>())
        {
            foreach (var name in dot > 0 ? new[] { file, file[..dot] } : [file])
            {
                if (store.IsValidName(name) && store.PathOf(kind, name) == path)
                    return (kind, name);
            }
        }

        return (null, null);
    }
}

/// <summary>An address a client could not load, as a client error told of it.</summary>
public sealed record BrokenAssetReport(string Url, int Status, string? IssueUrl);
