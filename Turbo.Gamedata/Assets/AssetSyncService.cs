using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
    AssetDiscoveryService discovery,
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

    private readonly SemaphoreSlim _listing = new(1, 1);
    private AssetDiscoveryResult? _lists;

    /// <summary>The background read <see cref="RelistBehind"/> started last; done when none runs.</summary>
    private Task _relisting = Task.CompletedTask;

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

    public AssetJobSnapshot StartCatalogPictures(PlayerId player, IReadOnlyCollection<string> names)
    {
        var pictures = HabboFileLists
            .CatalogImages([names])
            .Where(x => store.IsValidName(x.Name))
            .ToList();
        var domain = gamedataConfig.Value.HabboDomain;

        return jobs.Start(
            AssetJobKind.Sync,
            $"Catalog pictures from habbo.{domain}",
            player,
            async (progress, ct) =>
            {
                var known = await LoadKnownAsync(ct).ConfigureAwait(false);
                var tally = new Tally();

                // A picture is had by its name alone: no revision, nothing from the client.
                var context = new SyncContext(domain, HabboFileLists.STABLE, null, progress, tally);

                await SyncAsync("Catalog pictures", pictures, known, context, ct)
                    .ConfigureAwait(false);

                return tally.ToString();
            }
        );
    }

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
        await SaveMapEntriesAsync(lists.Figures ?? [], ct).ConfigureAwait(false);
        await SyncAsync("Effects", lists.Effects ?? [], known, context, ct).ConfigureAwait(false);
        await SaveMapEntriesAsync(lists.Effects ?? [], ct).ConfigureAwait(false);
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

    /// <summary>
    /// Habbo's lists as last read; read again when asked. Once older than the preview keeps them
    /// they are still answered with, and read again behind it: a visit doesn't wait on Habbo
    /// unless nothing was read yet.
    /// </summary>
    private async Task<AssetDiscoveryResult> ListHabboAsync(
        Action<string> log,
        bool probe,
        CancellationToken ct
    )
    {
        var lists = await discovery.DiscoverAsync(log, probe, ct).ConfigureAwait(false);
        _lists = lists;

        return lists;
    }

    private async Task<AssetDiscoveryResult> CachedListsAsync(bool relist, CancellationToken ct)
    {
        if (!relist && _lists is { } cached)
        {
            if (IsStale(cached))
                RelistBehind();

            return cached;
        }

        await _listing.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // Another request may have read them while this one waited.
            if (!relist && _lists is { } done)
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

    /// <summary>Reads Habbo's lists again in the background, unless a listing is under way.</summary>
    private void RelistBehind()
    {
        if (!_listing.Wait(0))
            return;

        _relisting = Task.Run(async () =>
        {
            try
            {
                await ListHabboAsync(_ => { }, probe: false, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Reading Habbo's lists again for the sync preview failed: {Message}",
                    ex.Message
                );
            }
            finally
            {
                _listing.Release();
            }
        });
    }

    /// <summary>
    /// Waits for a background read of Habbo's lists to end: what touches the database after a
    /// preview has answered, for whoever closes the database next.
    /// </summary>
#pragma warning disable VSTHRD003 // The read is this service's own, started on the thread pool by RelistBehind.
    internal Task WhenRelistedAsync() => _relisting;
#pragma warning restore VSTHRD003

    private bool IsStale(AssetDiscoveryResult lists) =>
        time.GetUtcNow().UtcDateTime - lists.ListedAt
        >= TimeSpan.FromMinutes(Math.Max(0, config.Value.SyncPreviewMinutes));

    /// <summary>
    /// Every library Habbo serves now, by kind, kept for the preview. A map Habbo has not, or that
    /// does not read, is null: that kind is left out rather than failing the rest.
    /// </summary>
    private async Task<byte[]?> AvatarActionsAsync(
        string domain,
        string revision,
        Action<string> log,
        CancellationToken ct
    )
    {
        var url = HabboAssetUrls.Gordon(config.Value, domain, revision, AVATAR_ACTIONS);

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
            AssetBundleKind.Furniture => HabboAssetUrls.Furniture(
                gamedataConfig.Value,
                context.Domain,
                library
            ),
            _ when !library.Kind.IsBundle() => HabboAssetUrls.File(
                config.Value,
                library.Kind,
                context.Domain,
                library.Name
            ),
            _ => HabboAssetUrls.Gordon(
                config.Value,
                context.Domain,
                context.Revision,
                $"{library.Name}.swf"
            ),
        };
        byte[]? data;

        try
        {
            // A library of Habbo's HTML client is read from the release, not downloaded.
            data =
                library.Download?.StartsWith(HabboAssetUrls.CLIENT_PREFIX, StringComparison.Ordinal)
                == true
                    ? context.Client?.Read(library.Download[HabboAssetUrls.CLIENT_PREFIX.Length..])
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
            return Outcome.Failed(library, HabboAssetUrls.NOT_A_FILE, retry: true);

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
            row.Error = GamedataStrings.Truncate(outcome.Error, AssetBundleEntity.ERROR_MAX_LENGTH);
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
    /// <summary>
    /// Each library's entries in Habbo's figure or effect map kept with its row, which the
    /// hotel's maps are built from (<see cref="ClientMaps"/>): a row whose entries changed is
    /// touched, so the map is built again. An upload keeps its own.
    /// </summary>
    private async Task SaveMapEntriesAsync(List<HabboLibrary> libraries, CancellationToken ct)
    {
        foreach (var chunk in libraries.Where(x => x.MapEntries is not null).Chunk(SAVE_BATCH))
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var kind = chunk[0].Kind;
            var entries = chunk.ToDictionary(
                x => x.Name,
                x => x.MapEntries,
                StringComparer.OrdinalIgnoreCase
            );
            var names = entries.Keys.ToList();
            var rows = await dbCtx
                .AssetBundles.Where(x =>
                    x.Kind == kind && x.Source != AssetBundleSource.Upload && names.Contains(x.Name)
                )
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var now = time.GetUtcNow().UtcDateTime;

            foreach (var row in rows.Where(x => x.MapEntries != entries[x.Name]))
            {
                row.MapEntries = entries[row.Name];
                row.UpdatedAt = now;
            }

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

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
        var onDisk = rows.Select(x => x.Kind).Distinct().ToDictionary(x => x, store.NamesOnDisk);

        return rows.ToDictionary(
            x => (x.Kind, x.Name),
            x => new AssetSyncPlan.Known(
                x.Revision,
                x.Source,
                x.HasHash && store.IsValidName(x.Kind, x.Name) && onDisk[x.Kind].Contains(x.Name),
                x.Error,
                x.Retry,
                x.Ids
            ),
            AssetSyncPlan.KEYS
        );
    }

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
