using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Database.Context;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>Reads Habbo's current asset lists and probes files that its maps do not name.</summary>
internal sealed class AssetDiscoveryService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<AssetBundleConfig> config,
    IOptions<GamedataConfig> gamedataConfig,
    HabboGamedataClient habbo,
    HabboClientService client,
    IAssetBundleStore store,
    TimeProvider time,
    ILogger<AssetDiscoveryService> logger
)
{
    private const int PROBE_BATCH = 6;
    private const int PROBE_MISSES = 25;
    private const int FIRST_PROBE_MISSES = 100;
    private const int BADGE_LEVEL_MISSES = 2;
    private const int BADGE_LEVEL_LIMIT = 50;

    /// <summary>
    /// Reads Habbo's external texts for posters and badges; unreadable texts leave those kinds
    /// out of this discovery run while the rest of the listing continues.
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

    internal async Task<AssetDiscoveryResult> DiscoverAsync(
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

        var listedAt = time.GetUtcNow().UtcDateTime;
        var (room, release) = await RoomAsync(revision, log, ct).ConfigureAwait(false);
        var lists = new AssetDiscoveryResult(
            domain,
            revision,
            listedAt,
            furniture,
            figures,
            effects,
            pets,
            room,
            texts is null
                ? null
                : await BadgesAsync(texts, domain, had[AssetBundleKind.Badge], probe, log, ct)
                    .ConfigureAwait(false),
            numbered[AssetBundleKind.CatalogIcon],
            await CatalogImagesAsync(ct).ConfigureAwait(false),
            numbered[AssetBundleKind.Sound]
        )
        {
            Client = release,
        };

        return lists;
    }

    /// <summary>
    /// The room's own libraries: each from Habbo's HTML client (<c>local_include/&lt;Name&gt;.hab</c>,
    /// at its file's SHA-256, so a changed one is taken again) when a release is downloaded and has
    /// it, as nitro-studio prefers it; else Habbo's gordon SWF at the client revision.
    /// </summary>
    private async Task<(List<HabboLibrary> Libraries, HabboClientRelease? Client)> RoomAsync(
        string revision,
        Action<string> log,
        CancellationToken ct
    )
    {
        HabboClientRelease? release = null;

        try
        {
            release = await client.OpenFollowedAsync(ct).ConfigureAwait(false);
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

        if (release is null)
            return (libraries, null);

        var fromClient = 0;

        for (var i = 0; i < libraries.Count; i++)
        {
            var path = $"local_include/{libraries[i].Name}.hab";

            if (release.FileHashes.TryGetValue(path, out var sha256))
            {
                libraries[i] = libraries[i] with
                {
                    Revision = sha256,
                    Download = HabboAssetUrls.CLIENT_PREFIX + path,
                };
                fromClient++;
            }
        }

        log($"{fromClient} room libraries come from Habbo's HTML client {release.Version}.");

        return (libraries, release);
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
                            habbo.ExistsAsync(
                                HabboAssetUrls.File(config.Value, set.Kind, domain, set.FileOf(n)),
                                ct
                            )
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
                                    HabboAssetUrls.File(
                                        config.Value,
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
            var url = HabboAssetUrls.File(
                config.Value,
                AssetBundleKind.Badge,
                domain,
                HabboFileLists.BadgeFile(casing)
            );

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
        var url = HabboAssetUrls.Gordon(config.Value, domain, revision, map);

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
}
