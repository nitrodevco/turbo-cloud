using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    IAssetBundleStore store,
    IAssetJobs jobs,
    TimeProvider time,
    ILogger<IAssetSyncService> logger
) : IAssetSyncService
{
    // Rows are written in batches: a first sync converts every library Habbo has.
    private const int SAVE_BATCH = 200;

    private const string NO_FILE = "Habbo has no file at its address.";
    private const string NOT_A_FILE =
        "Habbo answered with a page, not the file; its filter may have refused the request.";

    public AssetJobSnapshot Start(PlayerId player) =>
        jobs.Start(
            AssetJobKind.Sync,
            $"Sync from habbo.{gamedataConfig.Value.HabboDomain}",
            player,
            RunAsync
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
    /// The poster ids Habbo's external texts name. Without the texts, no poster is listed and the
    /// rest of the sync goes on: the posters come with the next sync that reads them.
    /// </summary>
    private async Task<IReadOnlyCollection<int>> PosterIdsAsync(
        string domain,
        IAssetJobProgress progress,
        CancellationToken ct
    )
    {
        try
        {
            var ids = FurnitureAssetNames.PosterIds(
                await habbo.GetExternalTextsAsync(domain, ct).ConfigureAwait(false)
            );

            progress.Log($"Habbo's texts name {ids.Length} posters.");

            return ids;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Habbo's external texts could not be read for its posters");
            progress.Log($"Posters left out: Habbo's texts could not be read ({ex.Message}).");

            return [];
        }
    }

    private async Task<string> RunAsync(IAssetJobProgress progress, CancellationToken ct)
    {
        progress.Step("Listing", 0);

        var domain = gamedataConfig.Value.HabboDomain;
        var variables = await habbo.GetExternalVariablesAsync(domain, ct).ConfigureAwait(false);
        var revision =
            HabboReleaseService.RevisionOf(variables)
            ?? throw new HttpRequestException(
                $"habbo.{domain}'s external variables name no client revision."
            );

        progress.Log($"habbo.{domain} serves client revision {revision}.");

        var furniture = HabboAssetLists.Furniture(
            await habbo.GetFurnitureDataAsync(domain, ct).ConfigureAwait(false),
            await PosterIdsAsync(domain, progress, ct).ConfigureAwait(false)
        );
        var figures = await ListFromMapAsync(
                domain,
                revision,
                HabboAssetLists.FIGURE_MAP,
                HabboAssetLists.Figures,
                progress,
                ct
            )
            .ConfigureAwait(false);
        var effects = await ListFromMapAsync(
                domain,
                revision,
                HabboAssetLists.EFFECT_MAP,
                HabboAssetLists.Effects,
                progress,
                ct
            )
            .ConfigureAwait(false);
        var pets = HabboAssetLists.Pets(
            variables.GetValueOrDefault(HabboAssetLists.PET_CONFIGURATION_VARIABLE),
            revision
        );

        progress.Log(
            $"Habbo lists {furniture.Count} furniture, {figures.Count} clothing, {effects.Count} effect and {pets.Count} pet libraries."
        );

        var known = await LoadKnownAsync(ct).ConfigureAwait(false);
        var tally = new Tally();
        var context = new SyncContext(domain, revision, progress, tally);

        await SyncAsync("Furniture", furniture, known, context, ct).ConfigureAwait(false);
        await SyncAsync("Clothing", figures, known, context, ct).ConfigureAwait(false);
        await SyncAsync("Effects", effects, known, context, ct).ConfigureAwait(false);
        await SyncAsync("Pets", pets, known, context, ct).ConfigureAwait(false);

        return tally.ToString();
    }

    /// <summary>
    /// The libraries a map of Habbo's (the figure map, the effect map) lists. A map Habbo has not,
    /// or that does not read, leaves that kind out of this sync rather than failing the rest.
    /// </summary>
    private async Task<List<HabboLibrary>> ListFromMapAsync(
        string domain,
        string revision,
        string map,
        Func<byte[], List<HabboLibrary>> parse,
        IAssetJobProgress progress,
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
            progress.Log($"Habbo has no {map} at {url}; its libraries are skipped.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Habbo's {Map} at {Url} could not be read", map, url);
            progress.Log($"{map} could not be read ({ex.Message}); its libraries are skipped.");
        }

        return [];
    }

    /// <summary>One kind's libraries: those that need it downloaded, converted and kept.</summary>
    private async Task SyncAsync(
        string phase,
        List<HabboLibrary> libraries,
        IReadOnlyDictionary<(AssetBundleKind, string), Known> known,
        SyncContext context,
        CancellationToken ct
    )
    {
        var todo = new List<HabboLibrary>();
        var idsChanged = new List<HabboLibrary>();
        var invalid = 0;

        foreach (var library in libraries)
        {
            if (!store.IsValidName(library.Name))
            {
                invalid++;

                continue;
            }

            if (known.TryGetValue((library.Kind, library.Name), out var row))
            {
                if (row.Source == AssetBundleSource.Upload)
                {
                    context.Tally.Uploads++;

                    continue;
                }

                if (
                    row.Revision == library.Revision
                    && (row.HasFile || (row.Error is not null && !row.Retry))
                )
                {
                    context.Tally.UpToDate++;

                    if (row.Ids != library.Ids)
                        idsChanged.Add(library);

                    continue;
                }
            }

            todo.Add(library);
        }

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
        var url =
            library.Kind == AssetBundleKind.Furniture
                ? FurnitureUrl(context.Domain, library)
                : GordonUrl(context.Domain, context.Revision, $"{library.Name}.swf");
        byte[]? data;

        try
        {
            data = await habbo.GetFileAsync(url, ct).ConfigureAwait(false);
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
        var byKey = existing.ToDictionary(x => (x.Kind, x.Name));

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
            var ids = chunk.ToDictionary(x => x.Name, x => x.Ids, StringComparer.Ordinal);
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

    private async Task<Dictionary<(AssetBundleKind, string), Known>> LoadKnownAsync(
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
                Known = new Known(x.Revision, x.Source, x.Hash != null, x.Error, x.Retry, x.Ids),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.ToDictionary(x => (x.Kind, x.Name), x => x.Known);
    }

    private string FurnitureUrl(string domain, HabboLibrary library) =>
        gamedataConfig
            .Value.FurnitureFileUrl.Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{revision}", library.Revision, StringComparison.Ordinal)
            .Replace("{name}", Uri.EscapeDataString(library.Name), StringComparison.Ordinal);

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

    /// <summary>What the hotel knows of a library before the sync.</summary>
    private sealed record Known(
        string? Revision,
        AssetBundleSource Source,
        bool HasFile,
        string? Error,
        bool Retry,
        string? Ids
    );

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
