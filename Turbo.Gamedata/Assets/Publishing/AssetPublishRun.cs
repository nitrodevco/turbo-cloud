using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// One publish to one target, as an asset job's work: sends what the target lacks or holds an
/// older copy of (by the hash recorded in <c>asset_published_files</c>), largest first and several
/// at once, each sender on its own connection; records what is sent as it goes, so a publish that
/// stops resumes where it was; and, when asked, deletes what the hotel no longer has. A file that
/// fails is logged and counted; the publish fails only when it can't connect at all.
/// </summary>
internal sealed class AssetPublishRun(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    PublishConnections connections,
    TimeProvider time,
    ILogger logger,
    AssetPublishTargetEntity target,
    string password,
    int concurrency,
    PlayerId player
)
{
    /// <summary>
    /// What is sent is saved this many files at a time, or after <see cref="RECORD_SECONDS"/>,
    /// whichever comes first: often enough that a publish that stops loses little, seldom enough
    /// that saving costs little.
    /// </summary>
    private const int RECORD_BATCH = 50;

    private const int RECORD_SECONDS = 5;

    private const double BYTES_PER_MEGABYTE = 1024d * 1024d;

    private const string STOPPED = "Stopped before it finished.";

    private int _uploaded;
    private int _failed;
    private int _deleted;
    private long _bytes;

    /// <summary>The bundles a target lacks or holds an older copy of, by what it recorded.</summary>
    public static IEnumerable<AssetBundleFile> ToSend(
        IEnumerable<AssetBundleFile> files,
        IReadOnlyDictionary<string, string> recorded
    ) => files.Where(x => !recorded.TryGetValue(x.Path, out var hash) || hash != x.Hash);

    public async Task<string> RunAsync(
        bool dryRun,
        bool deleteRemoved,
        IAssetJobProgress progress,
        CancellationToken ct
    )
    {
        progress.Step("Listing", 0);

        var files = await store.ListFilesAsync(ct).ConfigureAwait(false);
        var recorded = await LoadRecordedAsync(ct).ConfigureAwait(false);
        var send = ToSend(files, recorded).OrderByDescending(x => x.Size).ToList();
        var already = files.Count - send.Count;
        var present = files.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
        var delete = deleteRemoved
            ? recorded.Keys.Where(x => !present.Contains(x)).Order(StringComparer.Ordinal).ToList()
            : [];
        var historyId = await StartHistoryAsync(dryRun, ct).ConfigureAwait(false);

        if (dryRun)
        {
            var counted =
                $"{send.Count} to send ({Megabytes(send.Sum(x => x.Size))}), {already} already there, {delete.Count} to delete";

            progress.Log(counted);
            await FinishHistoryAsync(historyId, already, null).ConfigureAwait(false);

            return counted;
        }

        try
        {
            var first = await connections.ConnectAsync(target, password, ct).ConfigureAwait(false);

            await using (first.ConfigureAwait(false))
            {
                await TrustHostKeyAsync(dbCtxFactory, logger, target, first.HostKey, ct)
                    .ConfigureAwait(false);
                await UploadAsync(first, send, recorded, progress, ct).ConfigureAwait(false);

                if (delete.Count > 0)
                    await DeleteAsync(first, delete, progress, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishHistoryAsync(historyId, already, STOPPED).ConfigureAwait(false);

            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Publishing to {TargetId} ({Target}) failed",
                target.Id,
                PublishConnections.Where(target)
            );
            await FinishHistoryAsync(historyId, already, ex.Message).ConfigureAwait(false);

            throw;
        }

        var error = _failed > 0 ? $"{_failed} files could not be sent or deleted." : null;

        await FinishHistoryAsync(historyId, already, error).ConfigureAwait(false);

        var result =
            $"{_uploaded} sent ({Megabytes(_bytes)}), {already} already there, {_deleted} deleted";

        return _failed > 0 ? $"{result}, {_failed} failed" : result;
    }

    private static string Megabytes(long bytes) => $"{bytes / BYTES_PER_MEGABYTE:0.0} MB";

    private async Task<Dictionary<string, string>> LoadRecordedAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .AssetPublishedFiles.AsNoTracking()
            .Where(x => x.TargetEntityId == target.Id)
            .ToDictionaryAsync(x => x.Path, x => x.Hash, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
    }

    /// <summary>The SFTP host key a target is shown the first time is the one it trusts from then on.</summary>
    public static async Task TrustHostKeyAsync(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        ILogger logger,
        AssetPublishTargetEntity target,
        string? seen,
        CancellationToken ct
    )
    {
        if (target.HostKey is not null || seen is null)
            return;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        await dbCtx
            .AssetPublishTargets.Where(x => x.Id == target.Id && x.HostKey == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.HostKey, seen), ct)
            .ConfigureAwait(false);

        target.HostKey = seen;
        logger.LogInformation(
            "Publish target {TargetId} now trusts the host key {HostKey}",
            target.Id,
            seen
        );
    }

    private async Task UploadAsync(
        IPublishConnection first,
        IReadOnlyList<AssetBundleFile> send,
        IReadOnlyDictionary<string, string> recorded,
        IAssetJobProgress progress,
        CancellationToken ct
    )
    {
        progress.Step("Uploading", send.Count);

        if (send.Count == 0)
            return;

        var queue = new ConcurrentQueue<AssetBundleFile>(send);
        var recorder = new Recorder(dbCtxFactory, target.Id, time, recorded);
        var workers = Math.Clamp(concurrency, 1, send.Count);

        try
        {
            await Task.WhenAll(
                    Enumerable
                        .Range(0, workers)
                        .Select(i =>
                            SendAsync(i == 0 ? first : null, queue, recorder, progress, ct)
                        )
                )
                .ConfigureAwait(false);
        }
        finally
        {
            // Kept even when the publish stops: the next one resumes from it.
            await recorder.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>One sender: takes files off the queue until it is empty, on its own connection.</summary>
    private async Task SendAsync(
        IPublishConnection? shared,
        ConcurrentQueue<AssetBundleFile> queue,
        Recorder recorder,
        IAssetJobProgress progress,
        CancellationToken ct
    )
    {
        IPublishConnection connection;

        if (shared is not null)
            connection = shared;
        else
        {
            try
            {
                connection = await connections
                    .ConnectAsync(target, password, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The others carry on with the queue.
                logger.LogWarning(
                    ex,
                    "An extra connection to publish target {TargetId} could not be opened",
                    target.Id
                );
                progress.Log($"An extra connection could not be opened: {ex.Message}");

                return;
            }
        }

        try
        {
            var made = new HashSet<string>(StringComparer.Ordinal);

            while (queue.TryDequeue(out var file))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var folder = Path.GetDirectoryName(file.Path)?.Replace('\\', '/') ?? "";

                    if (made.Add(folder))
                        await connection.EnsureDirectoryAsync(folder, ct).ConfigureAwait(false);

                    var content = new FileStream(
                        store.FullPathOf(file.Kind, file.Name),
                        new FileStreamOptions
                        {
                            Mode = FileMode.Open,
                            Access = FileAccess.Read,
                            Share = FileShare.Read,
                            Options = FileOptions.Asynchronous,
                        }
                    );

                    await using (content.ConfigureAwait(false))
                        await connection.UploadAsync(content, file.Path, ct).ConfigureAwait(false);

                    Interlocked.Increment(ref _uploaded);
                    Interlocked.Add(ref _bytes, file.Size);
                    recorder.Add(file);
                    progress.Advance();
                }
                catch (Exception ex)
                    when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    logger.LogWarning(
                        ex,
                        "Sending {Path} to publish target {TargetId} failed",
                        file.Path,
                        target.Id
                    );
                    progress.Log($"{file.Path}: {ex.Message}");
                    Interlocked.Increment(ref _failed);
                    progress.Advance(failed: true);
                }

                await recorder.FlushIfDueAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (shared is null)
                await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task DeleteAsync(
        IPublishConnection connection,
        IReadOnlyList<string> delete,
        IAssetJobProgress progress,
        CancellationToken ct
    )
    {
        progress.Step("Deleting", delete.Count);

        var removed = new List<string>();

        try
        {
            foreach (var path in delete)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await connection.DeleteAsync(path, ct).ConfigureAwait(false);
                    removed.Add(path);
                    _deleted++;
                    progress.Advance();
                }
                catch (Exception ex)
                    when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    logger.LogWarning(
                        ex,
                        "Deleting {Path} from publish target {TargetId} failed",
                        path,
                        target.Id
                    );
                    progress.Log($"{path}: {ex.Message}");
                    _failed++;
                    progress.Advance(failed: true);
                }
            }
        }
        finally
        {
            await ForgetAsync(removed).ConfigureAwait(false);
        }
    }

    /// <summary>Drops the records of files deleted from the target. Not canceled: what was deleted is gone.</summary>
    private async Task ForgetAsync(IReadOnlyList<string> removed)
    {
        if (removed.Count == 0)
            return;

        var dbCtx = await dbCtxFactory
            .CreateDbContextAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        foreach (var chunk in removed.Chunk(RECORD_BATCH))
        {
            await dbCtx
                .AssetPublishedFiles.Where(x =>
                    x.TargetEntityId == target.Id && chunk.Contains(x.Path)
                )
                .ExecuteDeleteAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async Task<int> StartHistoryAsync(bool dryRun, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var entry = new AssetPublishEntity
        {
            TargetEntityId = target.Id,
            PlayerId = player.Value,
            DryRun = dryRun,
        };

        dbCtx.AssetPublishes.Add(entry);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        return entry.Id;
    }

    /// <summary>Writes how the publish ended. Not canceled: a publish that stops is recorded as stopped.</summary>
    private async Task FinishHistoryAsync(int historyId, int skipped, string? error)
    {
        var dbCtx = await dbCtxFactory
            .CreateDbContextAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var finishedAt = time.GetUtcNow().UtcDateTime;
        var trimmed = error is { Length: > AssetPublishEntity.ERROR_MAX_LENGTH }
            ? error[..AssetPublishEntity.ERROR_MAX_LENGTH]
            : error;

        await dbCtx
            .AssetPublishes.Where(x => x.Id == historyId)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.Uploaded, _uploaded)
                        .SetProperty(p => p.Skipped, skipped)
                        .SetProperty(p => p.Deleted, _deleted)
                        .SetProperty(p => p.Bytes, _bytes)
                        .SetProperty(p => p.Error, trimmed)
                        .SetProperty(p => p.FinishedAt, finishedAt),
                CancellationToken.None
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// What has been sent and not yet saved, saved in batches. Saving is never canceled: a file
    /// that reached the target is recorded even when the publish stops right after.
    /// </summary>
    private sealed class Recorder(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        int targetId,
        TimeProvider time,
        IReadOnlyDictionary<string, string> recorded
    )
    {
        private readonly ConcurrentQueue<AssetBundleFile> _sent = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private long _savedAt = time.GetTimestamp();

        public void Add(AssetBundleFile file) => _sent.Enqueue(file);

        public Task FlushIfDueAsync() =>
            _sent.Count >= RECORD_BATCH
            || time.GetElapsedTime(Interlocked.Read(ref _savedAt)).TotalSeconds >= RECORD_SECONDS
                ? FlushAsync()
                : Task.CompletedTask;

        public async Task FlushAsync()
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                Interlocked.Exchange(ref _savedAt, time.GetTimestamp());

                var batch = new List<AssetBundleFile>();

                while (_sent.TryDequeue(out var file))
                    batch.Add(file);

                if (batch.Count == 0)
                    return;

                await SaveAsync(batch).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task SaveAsync(List<AssetBundleFile> batch)
        {
            var dbCtx = await dbCtxFactory
                .CreateDbContextAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var known = batch.Where(x => recorded.ContainsKey(x.Path)).Select(x => x.Path).ToList();
            var existing =
                known.Count == 0
                    ? []
                    : await dbCtx
                        .AssetPublishedFiles.Where(x =>
                            x.TargetEntityId == targetId && known.Contains(x.Path)
                        )
                        .ToDictionaryAsync(
                            x => x.Path,
                            StringComparer.Ordinal,
                            CancellationToken.None
                        )
                        .ConfigureAwait(false);

            foreach (var file in batch)
            {
                if (existing.TryGetValue(file.Path, out var row))
                    row.Hash = file.Hash;
                else
                    dbCtx.AssetPublishedFiles.Add(
                        new AssetPublishedFileEntity
                        {
                            TargetEntityId = targetId,
                            Path = file.Path,
                            Hash = file.Hash,
                        }
                    );
            }

            await dbCtx.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
