using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Operations.Grains;
using Turbo.Operations.Scheduling;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Scheduling.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins.Scheduling;

/// <summary>
/// A server's side of the scheduler (docs/plugin-scheduler.md): what a run writes to the log, and
/// a per-server task, which every server runs on its own clock rather than one per tick for the
/// cluster.
/// </summary>
public sealed class ScheduledTaskHostTests : IDisposable
{
    private const string KEY = "rates.Reprice";

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(Now);
    private readonly ScheduledTaskGrain _grain;

    public ScheduledTaskHostTests()
    {
        _grain = GrainHarness.Attach(
            new ScheduledTaskGrain(_db, _clock, NullLogger<IScheduledTaskGrain>.Instance),
            _fakes
        );
        _fakes.Instances[(typeof(IScheduledTaskGrain), SingletonGrainId.GLOBAL)] = _grain;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Pins the 10-25 migration's measure: the routine run line was Debug, which the shipped
    /// <c>"Turbo": "Debug"</c> writes, so 8 tasks made 90% of a hotel's log (4,291 of 4,745 lines).
    /// </summary>
    [Fact]
    public async Task A_routine_run_logs_below_debug_and_the_first_run_and_a_run_now_say_so()
    {
        var logger = new CapturingLogger<ScheduledTaskHost>();
        var host = Host(logger, new ConcurrentQueue<DateTimeOffset>(), perServer: false);

        for (var i = 0; i < 3; i++)
            await RunAsync(host, manual: false);

        await RunAsync(host, manual: true);

        logger
            .Entries.Where(e => e.Message.StartsWith("Scheduled task " + KEY))
            .Select(e => e.Level)
            .Should()
            .Equal(LogLevel.Information, LogLevel.Trace, LogLevel.Trace, LogLevel.Information);
    }

    [Fact]
    public async Task A_per_server_task_runs_on_every_server_on_its_own_clock_and_on_run_now()
    {
        var runsA = new ConcurrentQueue<DateTimeOffset>();
        var runsB = new ConcurrentQueue<DateTimeOffset>();
        var a = Host(NullLogger<ScheduledTaskHost>.Instance, runsA, perServer: true);
        var b = Host(NullLogger<ScheduledTaskHost>.Instance, runsB, perServer: true);

        await a.SubscribeAsync(a, Ct);
        await b.SubscribeAsync(b, Ct);
        a.RunDuePerServer();
        b.RunDuePerServer();

        _clock.Advance(TimeSpan.FromSeconds(1));
        await _grain.TickAsync(Ct);
        a.RunDuePerServer();
        b.RunDuePerServer();
        await Task.WhenAll(a.IdleAsync(), b.IdleAsync());

        runsA.Should().Equal(Now.AddSeconds(1));
        runsB.Should().Equal([Now.AddSeconds(1)], "each server runs it, not one per cluster");

        var listed = (await _grain.ListAsync(Ct)).Single();
        listed.PerServer.Should().BeTrue();
        listed.Servers.Should().Be(2);

        (await _grain.RunNowAsync(KEY, Ct)).Should().Be(ScheduledTaskRunNow.Started);
        await Task.WhenAll(a.IdleAsync(), b.IdleAsync());
        runsA.Should().HaveCount(2);
        runsB.Should().HaveCount(2);
    }

    private ScheduledTaskHost Host(
        ILogger<ScheduledTaskHost> logger,
        ConcurrentQueue<DateTimeOffset> runs,
        bool perServer
    )
    {
        var host = new ScheduledTaskHost(_fakes.Create<Orleans.IGrainFactory>(), _clock, logger);
        IScheduledTask task = perServer ? new RepriceHere(runs) : new Reprice(runs);

        host.Register(
            "rates",
            [new ScheduledTaskDefinition(KEY, task, ScheduledTasks.AttributeOf(task))]
        );

        return host;
    }

    private async Task RunAsync(ScheduledTaskHost host, bool manual)
    {
        _clock.Advance(TimeSpan.FromSeconds(1));
        (await host.StartAsync(KEY, Guid.NewGuid(), _clock.GetUtcNow(), manual, Ct))
            .Should()
            .BeTrue();
        await host.IdleAsync();
    }

    [Every("00:00:01")]
    private sealed class Reprice(ConcurrentQueue<DateTimeOffset> runs) : IScheduledTask
    {
        public Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
        {
            runs.Enqueue(run.ScheduledFor);

            return Task.CompletedTask;
        }
    }

    [Every("00:00:01", PerServer = true)]
    private sealed class RepriceHere(ConcurrentQueue<DateTimeOffset> runs) : IScheduledTask
    {
        public Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
        {
            runs.Enqueue(run.ScheduledFor);

            return Task.CompletedTask;
        }
    }
}
