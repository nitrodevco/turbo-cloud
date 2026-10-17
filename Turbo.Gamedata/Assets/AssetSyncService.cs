using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// <see cref="IAssetSyncService"/>: lists what Habbo serves (<see cref="HabboAssetLists"/>), skips
/// what the hotel has at that revision (or failed at it in a way that won't pass) and every upload,
/// then downloads the rest <see cref="AssetBundleConfig.DownloadConcurrency"/> at a time, converts
/// it <see cref="AssetBundleConfig.ConvertConcurrency"/> at a time, writes each bundle through the
/// store and keeps its row. Rows are written in batches as they come, so a sync that stops part
/// way keeps what it did.
/// </summary>
internal sealed class AssetSyncService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<AssetBundleConfig> config,
    IOptions<GamedataConfig> gamedataConfig,
    HabboGamedataClient habbo,
    HabboClientService client,
    ClientBundleBuilder builder,
    IAssetBundleStore store,
    IAssetJobs jobs,
    TimeProvider time,
    ILogger<IAssetSyncService> logger
) : IAssetSyncService
{
    // Rows are written in batches: a first sync converts every library Habbo has.
    private const int SAVE_BATCH = 200;

    private const string AVATAR_ACTIONS = "HabboAvatarActions.xml";

    private const string NO_FILE = "Habbo has no file at its address.";

    /// <summary>A library's download name for one read from Habbo's HTML client: the path in the release follows.</summary>
    private const string CLIENT_PREFIX = "client:";

    // Numbered files are asked for this many at once, and given up on after this many missing in a row.
    private const int PROBE_BATCH = 6;
    private const int PROBE_MISSES = 25;
    private const int FIRST_PROBE_MISSES = 100;

    // An achievement's next levels are asked for until this many are missing, at most this far.
    private const int BADGE_LEVEL_MISSES = 2;
    private const int BADGE_LEVEL_LIMIT = 50;
    private const string NOT_A_FILE =
        "Habbo answered with a page, not the file; its filter may have refused the request.";

    private readonly SemaphoreSlim _listing = new(1, 1);
    private HabboLists? _lists;

    public AssetJobSnapshot Start(PlayerId player) =>
        jobs.Start(
            AssetJobKind.Sync,
            $"Sync from habbo.{gamedataConfig.Value.HabboDomain}",
            player,
            (progress, ct) => RunAsync(player, progress, ct)
        );

    public AssetJobSnapshot StartClientBuild(PlayerId player, bool force) =>
        jobs.Start(
            AssetJobKind.ClientBuild,
            "Build from Habbo's HTML client",
            player,
            async (progress, ct) =>
            {
                progress.Step("Client libraries", 0);

                var domain = gamedataConfig.Value.HabboDomain;
                string? revision = null;

                try
                {
                    revision = HabboReleaseService.RevisionOf(
                        await habbo.GetExternalVariablesAsync(domain, ct).ConfigureAwait(false)
                    );
                }
                catch (HttpRequestException ex)
                {
                    progress.Log(
                        $"habbo.{domain}'s external variables could not be read ({ex.Message})."
                    );
                }

                var actions = revision is null
                    ? null
                    : await AvatarActionsAsync(domain, revision, progress.Log, ct)
                        .ConfigureAwait(false);
                var built = await builder
                    .BuildAsync(actions, player, force, progress.Log, ct)
                    .ConfigureAwait(false);

                return $"{built} bundles built";
            }
        );

    public AssetJobSnapshot? StartAfterCheck(PlayerId player)
    {
        if (!config.Value.SyncAfterCheck)
            return null;

        if (jobs.Running)
        {
            logger.LogInformation(
                "No asset sync after the Habbo check: an asset job is running already"
            );

            return null;
        }

        try
        {
            var job = Start(player);

            logger.LogInformation(
                "Started asset sync {JobId} after a Habbo check by player {PlayerId}",
                job.Id,
                player
            );

            return job;
        }
        catch (InvalidOperationException ex)
        {
            // Another job started between the look and the start; the check stands.
            logger.LogInformation(ex, "No asset sync after the Habbo check: {Message}", ex.Message);

            return null;
        }
    }

    /// <summary>
    /// Habbo's external texts, which name its posters and badges; null when they could not be read,
    /// and the rest of the sync goes on without them: they come with the next sync that reads them.
    /// </summary>
    private async Task<byte[]?> TextsAsync(string domain, Action<string> log, CancellationToken ct)
    {
        try
        {
            return await habbo.GetExternalTextsAsync(domain, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Habbo's external texts could not be read for its posters and badges"
            );
            log($"Posters and badges left out: Habbo's texts could not be read ({ex.Message}).");

            return null;
        }
    }

    private async Task<string> RunAsync(
        PlayerId player,
        IAssetJobProgress progress,
        CancellationToken ct
    )
    {
        progress.Step("Listing", 0);

        var lists = await ListHabboAsync(progress.Log, probe: true, ct).ConfigureAwait(false);

        progress.Log(
            $"Habbo lists {lists.Furniture.Count} furniture, {lists.Figures?.Count ?? 0} clothing, {lists.Effects?.Count ?? 0} effect, {lists.Pets.Count} pet and {lists.Room.Count} room libraries, "
                + $"{lists.Badges?.Count ?? 0} badges, {lists.CatalogIcons.Count} catalog icons, {lists.CatalogImages.Count} catalog pictures and {lists.Sounds.Count} trax samples."
        );

        var known = await LoadKnownAsync(ct).ConfigureAwait(false);
        var tally = new Tally();
        var context = new SyncContext(lists.Domain, lists.Revision, lists.Client, progress, tally);

        await SyncAsync("Furniture", lists.Furniture, known, context, ct).ConfigureAwait(false);
        await SyncAsync("Clothing", lists.Figures ?? [], known, context, ct).ConfigureAwait(false);
        await SyncAsync("Effects", lists.Effects ?? [], known, context, ct).ConfigureAwait(false);
        await SyncAsync("Pets", lists.Pets, known, context, ct).ConfigureAwait(false);
        await SyncAsync("Room", lists.Room, known, context, ct).ConfigureAwait(false);
        await SyncAsync("Badges", lists.Badges ?? [], known, context, ct).ConfigureAwait(false);
        await SyncAsync("Catalog icons", lists.CatalogIcons, known, context, ct)
            .ConfigureAwait(false);
        await SyncAsync("Catalog pictures", lists.CatalogImages, known, context, ct)
            .ConfigureAwait(false);
        await SyncAsync("Trax samples", lists.Sounds, known, context, ct).ConfigureAwait(false);

        progress.Step("Client libraries", 0);
        await builder
            .BuildAsync(
                await AvatarActionsAsync(lists.Domain, lists.Revision, progress.Log, ct)
                    .ConfigureAwait(false),
                player,
                force: false,
                progress.Log,
                ct
            )
            .ConfigureAwait(false);

        return tally.ToString();
    }

    public async Task<AssetSyncPreview> PreviewAsync(bool relist, CancellationToken ct)
    {
        var lists = await CachedListsAsync(relist, ct).ConfigureAwait(false);
        var known = await LoadKnownAsync(ct).ConfigureAwait(false);
        var limit = Math.Max(0, config.Value.SyncPreviewItemLimit);

        AssetSyncKindPreview Preview(AssetBundleKind kind, List<HabboLibrary>? libraries)
        {
            var plan = AssetSyncPlan.Classify(libraries ?? [], known, store.IsValidName);
            var pending = plan
                .Todo.OrderBy(x => x.Reason)
                .ThenBy(x => x.Library.Name, StringComparer.Ordinal)
                .ToList();

            return new AssetSyncKindPreview
            {
                Kind = kind,
                Listed = libraries is not null,
                HabboLibraries = libraries?.Count ?? 0,
                UpToDate = plan.UpToDate,
                Uploads = plan.Uploads,
                New = pending.Count(x => x.Reason == AssetSyncReason.New),
                Updated = pending.Count(x => x.Reason == AssetSyncReason.Updated),
                Items =
                [
                    .. pending
                        .Take(limit)
                        .Select(x => new AssetSyncPendingItem
                        {
                            Name = x.Library.Name,
                            Reason = x.Reason,
                            HabboRevision = x.Library.Revision,
                            HotelRevision = x.Row?.Revision,
                            Error = x.Row?.Error,
                        }),
                ],
                Truncated = pending.Count > limit,
            };
        }

        return new AssetSyncPreview
        {
            Domain = lists.Domain,
            Revision = lists.Revision,
            ListedAt = lists.ListedAt,
            Kinds =
            [
                Preview(AssetBundleKind.Furniture, lists.Furniture),
                Preview(AssetBundleKind.Figure, lists.Figures),
                Preview(AssetBundleKind.Effect, lists.Effects),
                Preview(AssetBundleKind.Pet, lists.Pets),
                Preview(AssetBundleKind.Room, lists.Room),
                Preview(AssetBundleKind.Badge, lists.Badges),
                Preview(AssetBundleKind.CatalogIcon, lists.CatalogIcons),
                Preview(AssetBundleKind.CatalogImage, lists.CatalogImages),
                Preview(AssetBundleKind.Sound, lists.Sounds),
            ],
        };
    }

    /// <summary>Habbo's lists as last read; read again when asked, or once older than the preview keeps them.</summary>
    private async Task<HabboLists> CachedListsAsync(bool relist, CancellationToken ct)
    {
        if (!relist && Fresh(_lists) is { } cached)
            return cached;

        await _listing.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // Another request may have read them while this one waited.
            if (!relist && Fresh(_lists) is { } done)
                return done;

            // The numbered files and badge levels are asked for by a sync alone: the preview lists
            // what the last one found.
            return await ListHabboAsync(_ => { }, probe: false, ct).ConfigureAwait(false);
        }
        finally
        {
            _listing.Release();
        }
    }

    private HabboLists? Fresh(HabboLists? lists) =>
        lists is not null
        && time.GetUtcNow().UtcDateTime - lists.ListedAt
            < TimeSpan.FromMinutes(Math.Max(0, config.Value.SyncPreviewMinutes))
            ? lists
            : null;

    /// <summary>
    /// Every library Habbo serves now, by kind, kept for the preview. A map Habbo has not, or that
    /// does not read, is null: that kind is left out rather than failing the rest.
    /// </summary>
    private async Task<HabboLists> ListHabboAsync(
        Action<string> log,
        bool probe,
        CancellationToken ct
    )
    {
        var domain = gamedataConfig.Value.HabboDomain;
        var variables = await habbo.GetExternalVariablesAsync(domain, ct).ConfigureAwait(false);
        var revision =
            HabboReleaseService.RevisionOf(variables)
            ?? throw new HttpRequestException(
                $"habbo.{domain}'s external variables name no client revision."
            );

        log($"habbo.{domain} serves client revision {revision}.");

        var texts = await TextsAsync(domain, log, ct).ConfigureAwait(false);
        var posters = texts is null ? [] : FurnitureAssetNames.PosterIds(texts);

        if (texts is not null)
            log($"Habbo's texts name {posters.Length} posters.");

        var furniture = HabboAssetLists.Furniture(
            await habbo.GetFurnitureDataAsync(domain, ct).ConfigureAwait(false),
            posters
        );
        var figures = await ListFromMapAsync(
                domain,
                revision,
                HabboAssetLists.FIGURE_MAP,
                HabboAssetLists.Figures,
                log,
                ct
            )
            .ConfigureAwait(false);
        var effects = await ListFromMapAsync(
                domain,
                revision,
                HabboAssetLists.EFFECT_MAP,
                HabboAssetLists.Effects,
                log,
                ct
            )
            .ConfigureAwait(false);
        var pets = HabboAssetLists.Pets(
            variables.GetValueOrDefault(HabboAssetLists.PET_CONFIGURATION_VARIABLE),
            revision
        );
        var had = await HadFilesAsync(ct).ConfigureAwait(false);
        var numbered = new Dictionary<AssetBundleKind, List<HabboLibrary>>();

        foreach (var set in HabboFileLists.NUMBERED)
            numbered[set.Kind] = await NumberedAsync(set, domain, had[set.Kind], probe, log, ct)
                .ConfigureAwait(false);

        var lists = new HabboLists(
            domain,
            revision,
            time.GetUtcNow().UtcDateTime,
            furniture,
            figures,
            effects,
            pets,
            await RoomAsync(revision, log, ct).ConfigureAwait(false),
            texts is null
                ? null
                : await BadgesAsync(texts, domain, had[AssetBundleKind.Badge], probe, log, ct)
                    .ConfigureAwait(false),
            numbered[AssetBundleKind.CatalogIcon],
            await CatalogImagesAsync(ct).ConfigureAwait(false),
            numbered[AssetBundleKind.Sound]
        )
        {
            Client = _client,
        };

        _lists = lists;

        return lists;
    }

    /// <summary>The release of Habbo's HTML client the last listing opened.</summary>
    private HabboClientRelease? _client;

    /// <summary>
    /// The room's own libraries: each from Habbo's HTML client (<c>local_include/&lt;Name&gt;.hab</c>,
    /// at its file's SHA-256, so a changed one is taken again) when a release is downloaded and has
    /// it, as nitro-studio prefers it; else Habbo's gordon SWF at the client revision.
    /// </summary>
    private async Task<List<HabboLibrary>> RoomAsync(
        string revision,
        Action<string> log,
        CancellationToken ct
    )
    {
        _client = null;

        try
        {
            _client = await client.OpenFollowedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or AssetFormatException or JsonException)
        {
            logger.LogWarning(
                ex,
                "Habbo's HTML client could not be opened; the room libraries come from gordon"
            );
            log(
                $"Habbo's HTML client could not be opened ({ex.Message}); the room libraries come from gordon."
            );
        }

        var libraries = HabboFileLists.Room(revision);

        if (_client is null)
            return libraries;

        var fromClient = 0;

        for (var i = 0; i < libraries.Count; i++)
        {
            var path = $"local_include/{libraries[i].Name}.hab";

            if (_client.FileHashes.TryGetValue(path, out var sha256))
            {
                libraries[i] = libraries[i] with
                {
                    Revision = sha256,
                    Download = CLIENT_PREFIX + path,
                };
                fromClient++;
            }
        }

        log($"{fromClient} room libraries come from Habbo's HTML client {_client.Version}.");

        return libraries;
    }

    /// <summary>The files the hotel has of each kind of Habbo's that is found by asking for it, by name.</summary>
    private async Task<Dictionary<AssetBundleKind, HashSet<string>>> HadFilesAsync(
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        AssetBundleKind[] kinds =
        [
            AssetBundleKind.Badge,
            .. HabboFileLists.NUMBERED.Select(x => x.Kind),
        ];
        var rows = await dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => kinds.Contains(x.Kind) && x.Hash != null)
            .Select(x => new { x.Kind, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return kinds.ToDictionary(
            x => x,
            x => rows.Where(r => r.Kind == x).Select(r => r.Name).ToHashSet(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// A numbered set's files: those the hotel has, and - when <paramref name="probe"/> - each Habbo
    /// has above them, asked for in turn until <see cref="PROBE_MISSES"/> in a row are not there
    /// (<see cref="FIRST_PROBE_MISSES"/> when the hotel has none yet), as nitro-studio finds them.
    /// A question Habbo does not answer stops the asking; the next sync goes on from there.
    /// </summary>
    private async Task<List<HabboLibrary>> NumberedAsync(
        HabboFileLists.NumberedSet set,
        string domain,
        HashSet<string> had,
        bool probe,
        Action<string> log,
        CancellationToken ct
    )
    {
        var numbers = had.Select(set.NumberOf).OfType<int>().ToHashSet();

        if (probe)
        {
            var next = numbers.Count == 0 ? set.First : Math.Max(set.First, numbers.Max() + 1);
            var limit = numbers.Count == 0 ? FIRST_PROBE_MISSES : PROBE_MISSES;
            var misses = 0;
            var found = 0;
            var unanswered = false;

            while (misses < limit && !unanswered)
            {
                var batch = Enumerable.Range(next, PROBE_BATCH).ToList();
                var answers = await Task.WhenAll(
                        batch.Select(n =>
                            habbo.ExistsAsync(FileUrl(set.Kind, domain, set.FileOf(n)), ct)
                        )
                    )
                    .ConfigureAwait(false);

                for (var i = 0; i < batch.Count && misses < limit; i++)
                {
                    if (answers[i] is not { } there)
                    {
                        unanswered = true;

                        break;
                    }

                    if (there)
                    {
                        numbers.Add(batch[i]);
                        found++;
                        misses = 0;
                    }
                    else
                    {
                        misses++;
                    }
                }

                next += PROBE_BATCH;
            }

            log(
                $"{set.Pattern.Replace("{0}", "<n>", StringComparison.Ordinal)}: {found} new found by asking Habbo{(unanswered ? "; it stopped answering, so the next sync goes on from there" : "")}."
            );
        }

        return
        [
            .. numbers
                .Order()
                .Select(n => new HabboLibrary(
                    set.Kind,
                    set.FileOf(n),
                    HabboFileLists.STABLE,
                    null
                )),
        ];
    }

    /// <summary>
    /// The badges: each Habbo's texts name, each the hotel has, and - when <paramref name="probe"/> -
    /// the achievements' levels above the highest of each, asked for until
    /// <see cref="BADGE_LEVEL_MISSES"/> in a row are not there (at most
    /// <see cref="BADGE_LEVEL_LIMIT"/>), as nitro-studio finds the levels no text names.
    /// </summary>
    private async Task<List<HabboLibrary>> BadgesAsync(
        byte[] texts,
        string domain,
        HashSet<string> had,
        bool probe,
        Action<string> log,
        CancellationToken ct
    )
    {
        var codes = HabboFileLists.TextedBadges(texts);

        foreach (var name in had)
            codes.Add(Path.GetFileNameWithoutExtension(name));

        if (probe)
        {
            var highest = codes
                .Select(HabboFileLists.AchievementLevel)
                .OfType<(string Base, int Level)>()
                .GroupBy(x => x.Base, StringComparer.Ordinal)
                .Select(x => (Base: x.Key, Level: x.Max(y => y.Level)))
                .ToList();
            var found = new ConcurrentBag<string>();

            await Parallel
                .ForEachAsync(
                    highest,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PROBE_BATCH,
                        CancellationToken = ct,
                    },
                    async (achievement, token) =>
                    {
                        var misses = 0;

                        for (
                            var level = achievement.Level + 1;
                            level <= achievement.Level + BADGE_LEVEL_LIMIT
                                && misses < BADGE_LEVEL_MISSES;
                            level++
                        )
                        {
                            var code = $"{achievement.Base}{level}";
                            var there = await habbo
                                .ExistsAsync(
                                    FileUrl(
                                        AssetBundleKind.Badge,
                                        domain,
                                        HabboFileLists.BadgeFile(code)
                                    ),
                                    token
                                )
                                .ConfigureAwait(false);

                            if (there is not { } answered)
                                return;

                            if (answered)
                            {
                                found.Add(code);
                                misses = 0;
                            }
                            else
                            {
                                misses++;
                            }
                        }
                    }
                )
                .ConfigureAwait(false);

            foreach (var code in found)
                codes.Add(code);

            log($"Badges: {found.Count} achievement levels no text names found by asking Habbo.");
        }

        var kept = new List<string>(codes.Count);

        foreach (var casings in codes.GroupBy(x => x, StringComparer.OrdinalIgnoreCase))
            kept.Add(
                await OneCasingAsync([.. casings], domain, had, probe, ct).ConfigureAwait(false)
            );

        return [.. kept.Select(HabboFileLists.Badge).Where(x => store.IsValidName(x.Name))];
    }

    /// <summary>
    /// Habbo's texts name some badges twice, in casings that differ
    /// (<c>ACH_TetraBlokDailyWinner</c> and <c>ACH_TetrablokDailyWinner</c>), and its host serves
    /// only one of them; the hotel keeps a name once whatever its case. The one kept is the one the
    /// hotel has, else the first Habbo has, else - or when not asking - the first.
    /// </summary>
    private async Task<string> OneCasingAsync(
        List<string> casings,
        string domain,
        HashSet<string> had,
        bool probe,
        CancellationToken ct
    )
    {
        if (casings.Count == 1)
            return casings[0];

        var hotels = casings.FirstOrDefault(x => had.Contains(HabboFileLists.BadgeFile(x)));

        if (hotels is not null || !probe)
            return hotels ?? casings[0];

        foreach (var casing in casings)
        {
            var url = FileUrl(AssetBundleKind.Badge, domain, HabboFileLists.BadgeFile(casing));

            if (await habbo.ExistsAsync(url, ct).ConfigureAwait(false) == true)
                return casing;
        }

        return casings[0];
    }

    /// <summary>The headers and teasers the hotel's catalog pages name.</summary>
    private async Task<List<HabboLibrary>> CatalogImagesAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var pages = await dbCtx
            .CatalogPages.AsNoTracking()
            .Select(x => x.ImageData)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. HabboFileLists.CatalogImages(pages).Where(x => store.IsValidName(x.Name))];
    }

    /// <summary>
    /// The libraries a map of Habbo's (the figure map, the effect map) lists. A map Habbo has not,
    /// or that does not read, leaves that kind out of this sync rather than failing the rest.
    /// </summary>
    private async Task<List<HabboLibrary>?> ListFromMapAsync(
        string domain,
        string revision,
        string map,
        Func<byte[], List<HabboLibrary>> parse,
        Action<string> log,
        CancellationToken ct
    )
    {
        var url = GordonUrl(domain, revision, map);

        try
        {
            if (await habbo.GetFileAsync(url, ct).ConfigureAwait(false) is { } file)
                return parse(file);

            logger.LogWarning(
                "Habbo has no {Map} at {Url}; its libraries are not synced",
                map,
                url
            );
            log($"Habbo has no {map} at {url}; its libraries are skipped.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Habbo's {Map} at {Url} could not be read", map, url);
            log($"{map} could not be read ({ex.Message}); its libraries are skipped.");
        }

        return null;
    }

    /// <summary>
    /// Habbo's <c>HabboAvatarActions.xml</c>, which the client downloads from the gordon folder at its
    /// revision; null when it could not be read.
    /// </summary>
    private async Task<byte[]?> AvatarActionsAsync(
        string domain,
        string revision,
        Action<string> log,
        CancellationToken ct
    )
    {
        var url = GordonUrl(domain, revision, AVATAR_ACTIONS);

        try
        {
            var file = await habbo.GetFileAsync(url, ct).ConfigureAwait(false);

            if (
                file is not null
                && Encoding.UTF8.GetString(file, 0, Math.Min(file.Length, 512)).Contains("<actions")
            )
                return file;

            log($"Habbo has no {AVATAR_ACTIONS} at {url}.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Habbo's {File} at {Url} could not be read", AVATAR_ACTIONS, url);
            log($"{AVATAR_ACTIONS} could not be read ({ex.Message}).");
        }

        return null;
    }

    /// <summary>One kind's libraries: those that need it downloaded, converted and kept.</summary>
    private async Task SyncAsync(
        string phase,
        List<HabboLibrary> libraries,
        IReadOnlyDictionary<(AssetBundleKind, string), AssetSyncPlan.Known> known,
        SyncContext context,
        CancellationToken ct
    )
    {
        var plan = AssetSyncPlan.Classify(libraries, known, store.IsValidName);
        var todo = plan.Todo.Select(x => x.Library).ToList();
        var idsChanged = plan.IdsChanged;
        var invalid = plan.Invalid;

        context.Tally.Uploads += plan.Uploads;
        context.Tally.UpToDate += plan.UpToDate;

        if (invalid > 0)
        {
            logger.LogWarning(
                "{Count} {Phase} libraries Habbo lists have names a bundle can't have; they are skipped",
                invalid,
                phase
            );
            context.Progress.Log(
                $"{invalid} {phase.ToLowerInvariant()} names can't be bundles' and were skipped."
            );
        }

        context.Progress.Step(phase, todo.Count);

        await SaveIdsAsync(idsChanged, ct).ConfigureAwait(false);

        if (todo.Count == 0)
            return;

        // Workers download and convert; one writer keeps the rows as they come.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var converting = new SemaphoreSlim(Math.Max(1, config.Value.ConvertConcurrency));
        var done = Channel.CreateBounded<Outcome>(SAVE_BATCH * 2);
        var writer = WriteAsync(done.Reader, stop);

        try
        {
            await Parallel
                .ForEachAsync(
                    todo,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, config.Value.DownloadConcurrency),
                        CancellationToken = stop.Token,
                    },
                    async (library, token) =>
                    {
                        var outcome = await FetchAsync(library, context, converting, token)
                            .ConfigureAwait(false);

                        await done.Writer.WriteAsync(outcome, token).ConfigureAwait(false);

                        if (outcome.Error is null)
                        {
                            context.Tally.AddConverted();
                            context.Progress.Advance();
                        }
                        else
                        {
                            context.Tally.AddFailed();
                            context.Progress.Advance(failed: true);
                            context.Progress.Log($"{library.Name}: {outcome.Error}");
                        }
                    }
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Only the writer stops the workers while the job goes on: its failure is thrown below.
        }
        finally
        {
            done.Writer.TryComplete();
            await writer.ConfigureAwait(false);
        }
    }

    private async Task<Outcome> FetchAsync(
        HabboLibrary library,
        SyncContext context,
        SemaphoreSlim converting,
        CancellationToken ct
    )
    {
        var url = library.Kind switch
        {
            AssetBundleKind.Furniture => FurnitureUrl(context.Domain, library),
            _ when !library.Kind.IsBundle() => FileUrl(library.Kind, context.Domain, library.Name),
            _ => GordonUrl(context.Domain, context.Revision, $"{library.Name}.swf"),
        };
        byte[]? data;

        try
        {
            // A library of Habbo's HTML client is read from the release, not downloaded.
            data =
                library.Download?.StartsWith(CLIENT_PREFIX, StringComparison.Ordinal) == true
                    ? context.Client?.Read(library.Download[CLIENT_PREFIX.Length..])
                    : await habbo.GetFileAsync(url, ct).ConfigureAwait(false);
        }
        catch (AssetFormatException ex)
        {
            return Outcome.Failed(
                library,
                $"Habbo's HTML client could not be read: {ex.Message}",
                retry: true
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Asset library {Kind} {Name} ({Revision}) could not be downloaded from {Url}",
                library.Kind,
                library.Name,
                library.Revision,
                url
            );

            return Outcome.Failed(library, ex.Message, retry: true);
        }

        if (data is null)
            return Outcome.Failed(library, NO_FILE, retry: false);

        if (IsPage(data))
            return Outcome.Failed(library, NOT_A_FILE, retry: true);

        // A picture or a sound is kept as Habbo serves it.
        if (!library.Kind.IsBundle())
            return await KeepAsync(library, data, ct).ConfigureAwait(false);

        byte[] bundle;

        await converting.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            bundle = NitroConverter
                .Convert(data, library.Name, AssetBundleService.AssetTypeOf(library.Kind))
                .Write();
        }
        catch (AssetFormatException ex)
        {
            logger.LogWarning(
                ex,
                "Asset library {Kind} {Name} ({Revision}) does not convert",
                library.Kind,
                library.Name,
                library.Revision
            );

            return Outcome.Failed(library, ex.Message, retry: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A library the converter trips on must not stop the rest: it is kept as failed,
            // logged, and tried again by the next sync, by when the converter may take it.
            logger.LogError(
                ex,
                "Asset library {Kind} {Name} ({Revision}) could not be converted",
                library.Kind,
                library.Name,
                library.Revision
            );

            return Outcome.Failed(library, $"Could not be converted: {ex.Message}", retry: true);
        }
        finally
        {
            converting.Release();
        }

        return await KeepAsync(library, bundle, ct).ConfigureAwait(false);
    }

    private async Task<Outcome> KeepAsync(HabboLibrary library, byte[] bundle, CancellationToken ct)
    {
        try
        {
            var hash = await store
                .WriteAsync(library.Kind, library.Name, bundle, ct)
                .ConfigureAwait(false);

            return new Outcome(library, hash, bundle.LongLength, null, false);
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "Asset bundle {Kind} {Name} could not be written to the bundle folder",
                library.Kind,
                library.Name
            );

            return Outcome.Failed(library, $"Could not be written: {ex.Message}", retry: true);
        }
    }

    private async Task WriteAsync(ChannelReader<Outcome> outcomes, CancellationTokenSource stop)
    {
        var batch = new List<Outcome>(SAVE_BATCH);

        try
        {
            // Not the job's token: what was converted is kept even when the sync is stopped.
            await foreach (
                var outcome in outcomes.ReadAllAsync(CancellationToken.None).ConfigureAwait(false)
            )
            {
                batch.Add(outcome);

                if (batch.Count < SAVE_BATCH)
                    continue;

                await SaveAsync(batch).ConfigureAwait(false);
                batch.Clear();
            }

            await SaveAsync(batch).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Asset bundle rows could not be saved; the sync stops");
            await stop.CancelAsync().ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>Keeps the outcomes' rows, leaving alone a bundle uploaded while the sync ran.</summary>
    private async Task SaveAsync(List<Outcome> outcomes)
    {
        if (outcomes.Count == 0)
            return;

        var dbCtx = await dbCtxFactory
            .CreateDbContextAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var now = time.GetUtcNow().UtcDateTime;
        var kinds = outcomes.Select(x => x.Library.Kind).Distinct().ToList();
        var names = outcomes.Select(x => x.Library.Name).Distinct().ToList();
        var existing = await dbCtx
            .AssetBundles.Where(x => kinds.Contains(x.Kind) && names.Contains(x.Name))
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var byKey = existing.ToDictionary(x => (x.Kind, x.Name), AssetSyncPlan.KEYS);

        foreach (var outcome in outcomes)
        {
            var library = outcome.Library;

            if (!byKey.TryGetValue((library.Kind, library.Name), out var row))
            {
                row = new AssetBundleEntity
                {
                    Kind = library.Kind,
                    Name = library.Name,
                    Source = AssetBundleSource.Habbo,
                };
                dbCtx.AssetBundles.Add(row);
                byKey[(library.Kind, library.Name)] = row;
            }
            else if (row.Source == AssetBundleSource.Upload)
            {
                continue;
            }

            row.Revision = library.Revision;
            row.Ids = library.Ids;
            row.Error = Truncate(outcome.Error, AssetBundleEntity.ERROR_MAX_LENGTH);
            row.Retry = outcome.Retry;
            row.UpdatedAt = now;

            // A library that failed keeps the file of the revision before, when it had one.
            if (outcome.Hash is not null)
            {
                row.Hash = outcome.Hash;
                row.Size = outcome.Size;
            }
        }

        await dbCtx.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Brings up-to-date libraries' ids to what Habbo lists now, a batch at a time.</summary>
    private async Task SaveIdsAsync(List<HabboLibrary> libraries, CancellationToken ct)
    {
        foreach (var chunk in libraries.Chunk(SAVE_BATCH))
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var kind = chunk[0].Kind;
            var ids = chunk.ToDictionary(x => x.Name, x => x.Ids, StringComparer.OrdinalIgnoreCase);
            var names = ids.Keys.ToList();
            var rows = await dbCtx
                .AssetBundles.Where(x => x.Kind == kind && names.Contains(x.Name))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var row in rows)
                row.Ids = ids[row.Name];

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<(AssetBundleKind, string), AssetSyncPlan.Known>> LoadKnownAsync(
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .AssetBundles.AsNoTracking()
            .Select(x => new
            {
                x.Kind,
                x.Name,
                x.Revision,
                x.Source,
                HasHash = x.Hash != null,
                x.Error,
                x.Retry,
                x.Ids,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // A row's file, not its hash alone: a bundle whose file went (a folder lost, or moved
        // away) is downloaded again rather than counted as had.
        return rows.ToDictionary(
            x => (x.Kind, x.Name),
            x => new AssetSyncPlan.Known(
                x.Revision,
                x.Source,
                x.HasHash
                    && store.IsValidName(x.Name)
                    && File.Exists(store.FullPathOf(x.Kind, x.Name)),
                x.Error,
                x.Retry,
                x.Ids
            ),
            AssetSyncPlan.KEYS
        );
    }

    private string FurnitureUrl(string domain, HabboLibrary library) =>
        gamedataConfig
            .Value.FurnitureFileUrl.Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{revision}", library.Revision, StringComparison.Ordinal)
            .Replace(
                "{name}",
                Uri.EscapeDataString(library.Download ?? library.Name),
                StringComparison.Ordinal
            );

    /// <summary>Where one of Habbo's pictures or sounds is.</summary>
    private string FileUrl(AssetBundleKind kind, string domain, string name) =>
        (
            kind switch
            {
                AssetBundleKind.Badge => config.Value.BadgeFileUrl,
                AssetBundleKind.CatalogIcon or AssetBundleKind.CatalogImage => config
                    .Value
                    .CatalogFileUrl,
                AssetBundleKind.Sound => config.Value.SoundFileUrl,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            }
        )
            .Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{name}", Uri.EscapeDataString(name), StringComparison.Ordinal);

    private string GordonUrl(string domain, string revision, string name) =>
        config
            .Value.GordonFileUrl.Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{revision}", revision, StringComparison.Ordinal)
            .Replace("{name}", Uri.EscapeDataString(name), StringComparison.Ordinal);

    /// <summary>Whether Habbo answered with a page (its filter's refusal) rather than a library.</summary>
    private static bool IsPage(byte[] data)
    {
        foreach (var b in data)
        {
            if (char.IsWhiteSpace((char)b))
                continue;

            return b == (byte)'<';
        }

        return false;
    }

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max];

    /// <summary>Habbo's libraries by kind, as read at <see cref="ListedAt"/>; null where a map could not be read.</summary>
    private sealed record HabboLists(
        string Domain,
        string Revision,
        DateTime ListedAt,
        List<HabboLibrary> Furniture,
        List<HabboLibrary>? Figures,
        List<HabboLibrary>? Effects,
        List<HabboLibrary> Pets,
        List<HabboLibrary> Room,
        List<HabboLibrary>? Badges,
        List<HabboLibrary> CatalogIcons,
        List<HabboLibrary> CatalogImages,
        List<HabboLibrary> Sounds
    )
    {
        /// <summary>The release of Habbo's HTML client the room libraries were listed from, when one is downloaded.</summary>
        public HabboClientRelease? Client { get; init; }
    }

    /// <summary>What became of a library: its file's hash and size, or why it has none.</summary>
    private sealed record Outcome(
        HabboLibrary Library,
        string? Hash,
        long Size,
        string? Error,
        bool Retry
    )
    {
        public static Outcome Failed(HabboLibrary library, string error, bool retry) =>
            new(library, null, 0, error, retry);
    }

    private sealed record SyncContext(
        string Domain,
        string Revision,
        HabboClientRelease? Client,
        IAssetJobProgress Progress,
        Tally Tally
    );

    /// <summary>What the sync did, counted across its workers.</summary>
    private sealed class Tally
    {
        private int _converted;
        private int _failed;

        /// <summary>Libraries the hotel has at Habbo's revision; counted before the workers start.</summary>
        public int UpToDate { get; set; }

        /// <summary>Libraries left alone because staff uploaded them; counted before the workers start.</summary>
        public int Uploads { get; set; }

        public void AddConverted() => Interlocked.Increment(ref _converted);

        public void AddFailed() => Interlocked.Increment(ref _failed);

        public override string ToString() =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Volatile.Read(ref _converted)} converted, {Volatile.Read(ref _failed)} failed, {UpToDate} up to date, {Uploads} uploads kept"
            );
    }
}
