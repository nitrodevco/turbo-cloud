using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Api;

/// <summary>
/// Admin work that may outlast the request asking for it - a catalog reset, an import, a
/// rollback - which the panel's proxy would give up on long before it is done. The work runs on
/// its own token, never the request's, so it finishes whatever happens to the request. The
/// request waits <see cref="AdminConfig.JobWaitSeconds"/> for it and answers what the work
/// answers when it is done by then; otherwise it answers <c>202</c> with where to ask, and the
/// panel asks there, each request short, until the work's answer is there. A finished job is
/// kept <see cref="AdminConfig.JobKeepMinutes"/> for the staff member who started it.
/// </summary>
internal sealed class AdminJobs(
    IOptions<AdminConfig> config,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<AdminJobs> logger
)
{
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    /// <summary>
    /// Runs <paramref name="work"/>, which must not touch the request: read what it needs from
    /// the request first. Its answer is a JSON result (<c>Results.Ok</c>, <c>AdminResults.Error</c>)
    /// or no content, so it can be sent later as it would have been now.
    /// </summary>
    public async Task<IResult> RunAsync(
        PlayerId player,
        string title,
        Func<CancellationToken, Task<IResult>> work,
        CancellationToken requestCt
    )
    {
        Prune();

        var job = new Job(Guid.NewGuid(), title, player, time.GetUtcNow().UtcDateTime);

        _jobs[job.Id] = job;

        // Not the request's token: the work goes on when the panel's proxy has given up on it.
        var running = Task.Run(
            () => RunWorkAsync(job, work, lifetime.ApplicationStopping),
            CancellationToken.None
        );

        try
        {
            await running
                .WaitAsync(
                    TimeSpan.FromSeconds(Math.Max(0, config.Value.JobWaitSeconds)),
                    requestCt
                )
                .ConfigureAwait(false);
        }
        catch (TimeoutException) { }
        catch (OperationCanceledException) when (requestCt.IsCancellationRequested) { }

        if (job.Answer is { } answer)
        {
            _jobs.TryRemove(job.Id, out _);

            return answer;
        }

        return Results.Json(
            new AdminJobStartedResponse(job.Id, job.Title, $"/jobs/{job.Id}"),
            statusCode: StatusCodes.Status202Accepted
        );
    }

    /// <summary>The job as the staff member who started it sees it; null for anyone else, or when it is gone.</summary>
    public AdminJobResponse? Get(Guid id, PlayerId player)
    {
        Prune();

        if (!_jobs.TryGetValue(id, out var job) || job.Player != player)
            return null;

        var answer = job.Answer;

        return new AdminJobResponse(
            job.Id,
            job.Title,
            answer is not null,
            job.StartedAtUtc,
            job.FinishedAtUtc,
            answer is null
                ? null
                : (answer as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK,
            (answer as IValueHttpResult)?.Value
        );
    }

    private async Task RunWorkAsync(
        Job job,
        Func<CancellationToken, Task<IResult>> work,
        CancellationToken ct
    )
    {
        IResult answer;

        try
        {
            answer = await work(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The admin job {Title} ({JobId}) failed", job.Title, job.Id);
            answer = AdminResults.Error(
                StatusCodes.Status500InternalServerError,
                $"{job.Title} ran into a problem. It is in the server log."
            );
        }

        job.FinishedAtUtc = time.GetUtcNow().UtcDateTime;
        job.Answer = answer;
    }

    /// <summary>Forgets the jobs finished longer ago than they are kept.</summary>
    private void Prune()
    {
        var before = time.GetUtcNow()
            .UtcDateTime.AddMinutes(-Math.Max(0, config.Value.JobKeepMinutes));

        foreach (var job in _jobs.Values.Where(x => x.FinishedAtUtc < before))
            _jobs.TryRemove(job.Id, out _);
    }

    private sealed class Job(Guid id, string title, PlayerId player, DateTime startedAtUtc)
    {
        private IResult? _answer;

        public Guid Id { get; } = id;

        public string Title { get; } = title;

        public PlayerId Player { get; } = player;

        public DateTime StartedAtUtc { get; } = startedAtUtc;

        public DateTime? FinishedAtUtc { get; set; }

        /// <summary>Set once, last, when the work is done: a job with an answer is finished.</summary>
        public IResult? Answer
        {
            get => Volatile.Read(ref _answer);
            set => Volatile.Write(ref _answer, value);
        }
    }
}
