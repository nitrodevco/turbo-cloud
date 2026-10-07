using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Takes Habbo's releases in, in the background (<see cref="IGamedataImportJobs"/>): the asset
/// files its furniture needs that were not read yet (<see cref="HabboFurnitureFiles"/>), then the
/// definitions, which then have each file's states. The job is held in memory: the panel asks for
/// it while it runs, and a restart forgets it - the files read are kept, and the next import reads
/// only the rest. It stops with the server.
/// </summary>
internal sealed class FurnitureImportJobs(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGamedataFurnitureService furniture,
    IGamedataTextService texts,
    IGamedataProductService products,
    IGamedataFigureService figures,
    HabboFurnitureFiles files,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<FurnitureImportJobs> logger
) : IGamedataImportJobs
{
    private readonly Lock _lock = new();

    private GamedataImportJobSnapshot? _current;
    private int _filesDone;
    private int _filesFailed;

    public GamedataImportJobSnapshot? Current
    {
        get
        {
            lock (_lock)
                return _current is { Phase: GamedataImportPhase.Files } running
                    ? running with
                    {
                        FilesDone = Volatile.Read(ref _filesDone),
                        FilesFailed = Volatile.Read(ref _filesFailed),
                    }
                    : _current;
        }
    }

    public async Task<GamedataImportJobSnapshot?> StartAsync(
        int releaseId,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var release = await dbCtx
            .HabboReleases.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == releaseId, ct)
            .ConfigureAwait(false);

        if (release is null)
            return null;

        var known = await files.LoadAsync(ct).ConfigureAwait(false);
        var missing = HabboFurnitureFiles.Missing(
            HabboReleaseItems.Parse(release.FurnitureData).Values,
            known
        );

        GamedataImportJobSnapshot job;

        lock (_lock)
        {
            if (_current is { Phase: GamedataImportPhase.Files or GamedataImportPhase.Import })
                throw new InvalidOperationException(
                    $"Habbo {_current.Revision} is being taken in; wait for it to finish."
                );

            _filesDone = 0;
            _filesFailed = 0;
            _current = job = new GamedataImportJobSnapshot
            {
                ReleaseId = release.Id,
                Revision = release.Revision,
                File = GamedataFiles.FURNITURE_DATA,
                Phase = GamedataImportPhase.Files,
                FilesTotal = missing.Count,
                FilesDone = 0,
                FilesFailed = 0,
                StartedAt = time.GetUtcNow().UtcDateTime,
            };
        }

        // Not the request's token: the import outlives the request that started it.
        _ = Task.Run(
            () => RunAsync(job, missing, player, lifetime.ApplicationStopping),
            CancellationToken.None
        );

        return job;
    }

    public async Task<GamedataImportJobSnapshot?> StartTextsAsync(
        int versionId,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await dbCtx
            .HabboTextVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == versionId, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        GamedataImportJobSnapshot job;

        lock (_lock)
        {
            if (_current is { Phase: GamedataImportPhase.Files or GamedataImportPhase.Import })
                throw new InvalidOperationException(
                    "Something of Habbo's is being taken in; wait for it to finish."
                );

            _filesDone = 0;
            _filesFailed = 0;
            _current = job = new GamedataImportJobSnapshot
            {
                ReleaseId = version.Id,
                Revision = $"texts {version.Hash[..10]}",
                File = GamedataFiles.EXTERNAL_TEXTS,
                Phase = GamedataImportPhase.Import,
                FilesTotal = 0,
                FilesDone = 0,
                FilesFailed = 0,
                StartedAt = time.GetUtcNow().UtcDateTime,
            };
        }

        // Not the request's token: the import outlives the request that started it.
        _ = Task.Run(
            () => RunTextsAsync(job, player, lifetime.ApplicationStopping),
            CancellationToken.None
        );

        return job;
    }

    public async Task<GamedataImportJobSnapshot?> StartProductsAsync(
        int versionId,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await dbCtx
            .HabboProductVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == versionId, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        var job = Begin(
            version.Id,
            $"product data {version.Hash[..10]}",
            GamedataFiles.PRODUCT_DATA
        );

        // Not the request's token: the import outlives the request that started it.
        _ = Task.Run(
            () =>
                RunSimpleAsync(
                    job,
                    token => products.ImportAsync(job.ReleaseId, player, token),
                    lifetime.ApplicationStopping
                ),
            CancellationToken.None
        );

        return job;
    }

    public async Task<GamedataImportJobSnapshot?> StartFiguresAsync(
        int versionId,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await dbCtx
            .HabboFigureVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == versionId, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        var job = Begin(version.Id, $"figure data {version.Hash[..10]}", GamedataFiles.FIGURE_DATA);

        // Not the request's token: the import outlives the request that started it.
        _ = Task.Run(
            () =>
                RunSimpleAsync(
                    job,
                    token => figures.ImportAsync(job.ReleaseId, player, token),
                    lifetime.ApplicationStopping
                ),
            CancellationToken.None
        );

        return job;
    }

    /// <summary>A job of one step (texts, product data, figure data), begun when no other import runs.</summary>
    private GamedataImportJobSnapshot Begin(int id, string revision, string file)
    {
        lock (_lock)
        {
            if (_current is { Phase: GamedataImportPhase.Files or GamedataImportPhase.Import })
                throw new InvalidOperationException(
                    "Something of Habbo's is being taken in; wait for it to finish."
                );

            _filesDone = 0;
            _filesFailed = 0;
            _current = new GamedataImportJobSnapshot
            {
                ReleaseId = id,
                Revision = revision,
                File = file,
                Phase = GamedataImportPhase.Import,
                FilesTotal = 0,
                FilesDone = 0,
                FilesFailed = 0,
                StartedAt = time.GetUtcNow().UtcDateTime,
            };

            return _current;
        }
    }

    private async Task RunSimpleAsync(
        GamedataImportJobSnapshot job,
        Func<CancellationToken, Task<GamedataChangeSetSnapshot?>> import,
        CancellationToken ct
    )
    {
        try
        {
            var changeSet = await import(ct).ConfigureAwait(false);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Done,
                        ChangeSet = changeSet,
                        FinishedAt = time.GetUtcNow().UtcDateTime,
                    }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Taking in Habbo's {File} {Id} failed", job.File, job.ReleaseId);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Failed,
                        Error = ct.IsCancellationRequested
                            ? "The server stopped before it finished."
                            : ex.Message,
                        FinishedAt = time.GetUtcNow().UtcDateTime,
                    }
            );
        }
    }

    private async Task RunTextsAsync(
        GamedataImportJobSnapshot job,
        PlayerId player,
        CancellationToken ct
    )
    {
        try
        {
            var changeSet = await texts
                .ImportAsync(job.ReleaseId, player, ct)
                .ConfigureAwait(false);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Done,
                        ChangeSet = changeSet,
                        FinishedAt = time.GetUtcNow().UtcDateTime,
                    }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Taking in Habbo's texts {VersionId} failed", job.ReleaseId);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Failed,
                        Error = ct.IsCancellationRequested
                            ? "The server stopped before it finished."
                            : ex.Message,
                        FinishedAt = time.GetUtcNow().UtcDateTime,
                    }
            );
        }
    }

    private async Task RunAsync(
        GamedataImportJobSnapshot job,
        System.Collections.Generic.List<(string Asset, int Revision)> missing,
        PlayerId player,
        CancellationToken ct
    )
    {
        try
        {
            logger.LogInformation(
                "Taking in Habbo {Revision}: reading {Count} furniture files first",
                job.Revision,
                missing.Count
            );

            await files
                .FetchAsync(
                    missing,
                    failed =>
                    {
                        Interlocked.Increment(ref _filesDone);

                        if (failed)
                            Interlocked.Increment(ref _filesFailed);
                    },
                    ct
                )
                .ConfigureAwait(false);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Import,
                        FilesDone = _filesDone,
                        FilesFailed = _filesFailed,
                    }
            );

            var changeSet = await furniture
                .ImportAsync(job.ReleaseId, player, ct)
                .ConfigureAwait(false);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Done,
                        ChangeSet = changeSet,
                        FinishedAt = time.GetUtcNow().UtcDateTime,
                    }
            );

            logger.LogInformation(
                "Took in Habbo {Revision}: {Files} files read, {Failed} could not be",
                job.Revision,
                _filesDone,
                _filesFailed
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Taking in Habbo {Revision} failed", job.Revision);

            Update(
                job,
                x =>
                    x with
                    {
                        Phase = GamedataImportPhase.Failed,
                        FilesDone = _filesDone,
                        FilesFailed = _filesFailed,
                        Error = ct.IsCancellationRequested
                            ? "The server stopped before it finished."
                            : ex.Message,
                        FinishedAt = time.GetUtcNow().UtcDateTime,
                    }
            );
        }
    }

    private void Update(
        GamedataImportJobSnapshot job,
        Func<GamedataImportJobSnapshot, GamedataImportJobSnapshot> change
    )
    {
        lock (_lock)
            if (
                _current is { } current
                && current.StartedAt == job.StartedAt
                && current.ReleaseId == job.ReleaseId
            )
                _current = change(current);
    }
}
