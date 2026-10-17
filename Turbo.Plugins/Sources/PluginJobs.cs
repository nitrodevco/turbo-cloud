using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins.Sources;

/// <summary>
/// What a plugin job's work reports through: the step it is on (of the steps it has) and lines
/// for its log.
/// </summary>
internal interface IPluginJobProgress
{
    void Step(string phase);

    void Log(string line);
}

/// <summary>
/// The plugin jobs (a check of the linked repositories, an update from one), one at a time and in
/// the background, as the asset jobs are: the panel asks for <see cref="Current"/> while one runs.
/// Held in memory; a restart forgets the job, and what it finished is kept.
/// </summary>
internal sealed class PluginJobs(
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<PluginJobs> logger
)
{
    /// <summary>The newest log lines a job keeps.</summary>
    public const int LOG_LIMIT = 200;

    private readonly Lock _lock = new();
    private readonly Queue<string> _log = new();

    private PluginJobSnapshot? _current;
    private CancellationTokenSource? _cancel;
    private string _phase = "";
    private int _done;

    public PluginJobSnapshot? Current
    {
        get
        {
            lock (_lock)
                return _current is null
                    ? null
                    : _current with
                    {
                        Phase = _phase,
                        Done = _done,
                        Log = [.. _log],
                    };
        }
    }

    public bool Running
    {
        get
        {
            lock (_lock)
                return _current?.Status == PluginJobStatus.Running;
        }
    }

    /// <summary>
    /// Starts <paramref name="work"/> as a job of <paramref name="steps"/> steps and returns at once.
    /// The work returns its result in a line, or throws to fail. Throws
    /// <see cref="InvalidOperationException"/> while another job runs.
    /// </summary>
    public PluginJobSnapshot Start(
        PluginJobKind kind,
        string title,
        int steps,
        int player,
        Func<IPluginJobProgress, CancellationToken, Task<string>> work
    )
    {
        CancellationTokenSource cancel;
        PluginJobSnapshot job;

        lock (_lock)
        {
            if (_current?.Status == PluginJobStatus.Running)
                throw new InvalidOperationException(
                    $"{_current.Title} is running; wait for it to finish, or stop it."
                );

            _cancel?.Dispose();
            _cancel = cancel = CancellationTokenSource.CreateLinkedTokenSource(
                lifetime.ApplicationStopping
            );
            _log.Clear();
            _phase = "Starting";
            _done = 0;
            _current = job = new PluginJobSnapshot(
                Guid.NewGuid(),
                kind,
                title,
                PluginJobStatus.Running,
                _phase,
                steps,
                0,
                [],
                null,
                null,
                player,
                time.GetUtcNow().UtcDateTime,
                null
            );
        }

        logger.LogInformation("Player {PlayerId} started the plugin job {Title}", player, title);

        // Not the request's token: the job outlives the request that started it.
        _ = Task.Run(() => RunAsync(job.Id, work, cancel.Token), CancellationToken.None);

        return job;
    }

    public bool Cancel()
    {
        lock (_lock)
        {
            if (_current?.Status != PluginJobStatus.Running || _cancel is null)
                return false;

            _cancel.Cancel();

            return true;
        }
    }

    private async Task RunAsync(
        Guid id,
        Func<IPluginJobProgress, CancellationToken, Task<string>> work,
        CancellationToken ct
    )
    {
        try
        {
            var result = await work(new Progress(this, id), ct).ConfigureAwait(false);

            Finish(id, PluginJobStatus.Done, result, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Finish(id, PluginJobStatus.Canceled, null, "Stopped before it finished.");
        }
        catch (Exception ex)
        {
            if (ex is not GitHubException and not PluginJobFailure)
                logger.LogError(ex, "The plugin job {Id} failed", id);

            Finish(id, PluginJobStatus.Failed, null, ex.Message);
        }
    }

    private void Finish(Guid id, PluginJobStatus status, string? result, string? error)
    {
        lock (_lock)
        {
            if (_current?.Id != id)
                return;

            if (status == PluginJobStatus.Done)
                _done = _current.Total;

            if (error is not null)
                Add(error);
            else if (result is not null)
                Add(result);

            _current = _current with
            {
                Status = status,
                Result = result,
                Error = error,
                FinishedAt = time.GetUtcNow().UtcDateTime,
            };
        }
    }

    private void Add(string line)
    {
        _log.Enqueue(line);

        while (_log.Count > LOG_LIMIT)
            _log.Dequeue();
    }

    private sealed class Progress(PluginJobs jobs, Guid id) : IPluginJobProgress
    {
        public void Step(string phase)
        {
            lock (jobs._lock)
            {
                if (jobs._current?.Id != id)
                    return;

                if (jobs._phase != "Starting")
                    jobs._done = Math.Min(jobs._done + 1, jobs._current.Total);

                jobs._phase = phase;
                jobs.Add(phase);
            }
        }

        public void Log(string line)
        {
            lock (jobs._lock)
                if (jobs._current?.Id == id)
                    jobs.Add(line);
        }
    }
}

/// <summary>A job that could not do what it was for, with why, for the panel (not an error in the server).</summary>
internal sealed class PluginJobFailure(string message) : Exception(message);
