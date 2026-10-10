using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// <see cref="IAssetJobs"/>: one job at a time, held in memory. Its counters move as the work
/// reports and are read whole by <see cref="Current"/>; the work runs on the thread pool with a
/// token that <see cref="Cancel"/> and the server stopping both cancel.
/// </summary>
internal sealed class AssetJobs(
    IOptions<AssetBundleConfig> config,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<IAssetJobs> logger
) : IAssetJobs
{
    private readonly int _logLimit = Math.Max(1, config.Value.JobLogLimit);
    private readonly Lock _lock = new();
    private readonly Queue<string> _log = new();

    private AssetJobSnapshot? _current;
    private CancellationTokenSource? _cancel;
    private string _phase = "";
    private int _total;
    private int _done;
    private int _failed;

    public AssetJobSnapshot? Current
    {
        get
        {
            lock (_lock)
            {
                return _current is null
                    ? null
                    : _current with
                    {
                        Phase = _phase,
                        Total = _total,
                        Done = _done,
                        Failed = _failed,
                        Log = [.. _log],
                    };
            }
        }
    }

    public bool Running
    {
        get
        {
            lock (_lock)
                return _current?.Status == AssetJobStatus.Running;
        }
    }

    public AssetJobSnapshot Start(
        AssetJobKind kind,
        string title,
        PlayerId player,
        Func<IAssetJobProgress, CancellationToken, Task<string>> work
    )
    {
        CancellationTokenSource cancel;
        AssetJobSnapshot job;

        lock (_lock)
        {
            if (_current?.Status == AssetJobStatus.Running)
                throw new InvalidOperationException(
                    $"{_current.Title} is running; wait for it to finish, or stop it."
                );

            _cancel?.Dispose();
            _cancel = cancel = CancellationTokenSource.CreateLinkedTokenSource(
                lifetime.ApplicationStopping
            );
            _log.Clear();
            _phase = "Starting";
            _total = 0;
            _done = 0;
            _failed = 0;
            _current = job = new AssetJobSnapshot
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Title = title,
                Status = AssetJobStatus.Running,
                Phase = _phase,
                Total = 0,
                Done = 0,
                Failed = 0,
                Log = [],
                PlayerId = player.Value,
                StartedAt = time.GetUtcNow().UtcDateTime,
            };
        }

        logger.LogInformation("Player {PlayerId} started the asset job {Title}", player, title);

        // Not the request's token: the job outlives the request that started it.
        _ = Task.Run(() => RunAsync(job.Id, work, cancel.Token), CancellationToken.None);

        return job;
    }

    public bool Cancel()
    {
        lock (_lock)
        {
            if (_current?.Status != AssetJobStatus.Running || _cancel is null)
                return false;

            _cancel.Cancel();

            return true;
        }
    }

    private async Task RunAsync(
        Guid id,
        Func<IAssetJobProgress, CancellationToken, Task<string>> work,
        CancellationToken ct
    )
    {
        var progress = new Progress(this, id);

        try
        {
            var result = await work(progress, ct).ConfigureAwait(false);

            Finish(id, AssetJobStatus.Done, result, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Finish(id, AssetJobStatus.Canceled, null, "Stopped before it finished.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The asset job {JobId} failed", id);
            Finish(id, AssetJobStatus.Failed, null, ex.Message);
        }
    }

    private void Finish(Guid id, AssetJobStatus status, string? result, string? error)
    {
        lock (_lock)
        {
            if (_current?.Id != id)
                return;

            _current = _current with
            {
                Status = status,
                Result = result,
                Error = error,
                FinishedAt = time.GetUtcNow().UtcDateTime,
            };
        }

        logger.LogInformation(
            "The asset job {JobId} ended {Status}: {Result}",
            id,
            status,
            result ?? error
        );
    }

    /// <summary>The work's reports, applied while the job is still the one running.</summary>
    private sealed class Progress(AssetJobs jobs, Guid id) : IAssetJobProgress
    {
        public void Step(string phase, int total)
        {
            lock (jobs._lock)
            {
                if (jobs._current?.Id != id)
                    return;

                jobs._phase = phase;
                jobs._total = Math.Max(0, total);
                jobs._done = 0;
            }
        }

        public void Advance(bool failed = false)
        {
            lock (jobs._lock)
            {
                if (jobs._current?.Id != id)
                    return;

                jobs._done++;

                if (failed)
                    jobs._failed++;
            }
        }

        public void Log(string line)
        {
            lock (jobs._lock)
            {
                if (jobs._current?.Id != id)
                    return;

                jobs._log.Enqueue(line);

                while (jobs._log.Count > jobs._logLimit)
                    jobs._log.Dequeue();
            }
        }
    }
}
