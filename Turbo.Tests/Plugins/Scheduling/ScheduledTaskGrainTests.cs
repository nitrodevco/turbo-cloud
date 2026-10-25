using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Scheduling;
using Turbo.Operations.Grains;
using Turbo.Primitives.Scheduling.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins.Scheduling;

/// <summary>
/// The scheduler grain's rules (docs/plugin-scheduler.md): a run missed while no server was up is
/// skipped unless the task asks for it, a run due while the one before goes is skipped, run now
/// does not move the schedule, and the last run is kept so a restart counts from it.
/// </summary>
public sealed class ScheduledTaskGrainTests : IDisposable
{
    private const string KEY = "rares.WeeklyRotation";
    private const string MONDAYS = "0 18 * * MON UTC";

    // Sunday 25 October 2026: the run due Monday 19 October was missed, the next is Monday 26.
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(Now);
    private readonly RecordingServer _server = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_run_missed_while_no_server_was_up_runs_once_at_start_only_when_the_task_asks(
        bool runMissed
    )
    {
        _db.Insert(
            new ScheduledTaskRunEntity
            {
                Key = KEY,
                ScheduledForUtc = new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc),
                StartedAtUtc = new DateTime(2026, 10, 12, 18, 0, 1, DateTimeKind.Utc),
                Outcome = nameof(ScheduledTaskOutcome.Succeeded),
            }
        );

        var grain = Grain();
        await SubscribeAsync(grain, MONDAYS, runMissed);
        await grain.TickAsync(Ct);

        if (runMissed)
        {
            _server.Starts.Should().ContainSingle().Which.ScheduledFor.Should().Be(Now);
            (await grain.ListAsync(Ct))
                .Single()
                .NextRunAt.Should()
                .Be(
                    new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero),
                    "one run, not one per missed week"
                );
        }
        else
        {
            _server.Starts.Should().BeEmpty();
            (await grain.ListAsync(Ct))
                .Single()
                .NextRunAt.Should()
                .Be(new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero));
        }
    }

    [Fact]
    public async Task A_run_due_while_the_one_before_still_goes_is_skipped_not_started_twice()
    {
        var grain = Grain();
        await SubscribeAsync(grain, "00:00:01");

        _clock.Advance(TimeSpan.FromSeconds(1));
        await grain.TickAsync(Ct);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await grain.TickAsync(Ct);

        _server.Starts.Should().ContainSingle();
        var listed = (await grain.ListAsync(Ct)).Single();
        listed.Skipped.Should().Be(1);
        listed.RunningSince.Should().NotBeNull();

        // It ends: the next one due starts.
        await grain.CompletedAsync(
            KEY,
            _server.Starts[0].RunId,
            Result(ScheduledTaskOutcome.Succeeded),
            Ct
        );
        _clock.Advance(TimeSpan.FromSeconds(1));
        await grain.TickAsync(Ct);

        _server.Starts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Run_now_runs_at_once_keeps_the_schedule_and_the_last_run_outlives_the_grain()
    {
        var grain = Grain();
        await SubscribeAsync(grain, MONDAYS);

        (await grain.RunNowAsync(KEY, Ct)).Should().Be(ScheduledTaskRunNow.Started);
        (await grain.RunNowAsync(KEY, Ct)).Should().Be(ScheduledTaskRunNow.AlreadyRunning);
        (await grain.RunNowAsync("rares.Nothing", Ct)).Should().Be(ScheduledTaskRunNow.Unknown);
        _server.Starts.Should().ContainSingle().Which.Manual.Should().BeTrue();

        await grain.CompletedAsync(
            KEY,
            _server.Starts[0].RunId,
            Result(ScheduledTaskOutcome.Failed, "boom"),
            Ct
        );

        var listed = (await grain.ListAsync(Ct)).Single();
        listed.NextRunAt.Should().Be(new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero));
        listed.LastRun!.Outcome.Should().Be(ScheduledTaskOutcome.Failed);
        listed.Description.Should().Be("Mondays 18:00 (UTC)");

        // A new activation (a restart) reads it back.
        var again = Grain();
        await SubscribeAsync(again, MONDAYS);

        var reread = (await again.ListAsync(Ct)).Single();
        reread.LastRun.Should().BeEquivalentTo(listed.LastRun);
        reread.NextRunAt.Should().Be(listed.NextRunAt);
    }

    [Fact]
    public async Task A_server_that_stops_answering_loses_its_run_and_its_tasks()
    {
        var grain = Grain();
        await SubscribeAsync(grain, MONDAYS);
        await grain.RunNowAsync(KEY, Ct);

        _clock.Advance(ScheduledTaskTimes.ServerTimeout + TimeSpan.FromSeconds(1));
        await grain.TickAsync(Ct);

        (await grain.ListAsync(Ct)).Should().BeEmpty();
        using var db = _db.CreateDbContext();
        db.ScheduledTaskRuns.Single().Outcome.Should().Be(nameof(ScheduledTaskOutcome.Lost));
    }

    private ScheduledTaskGrain Grain() =>
        GrainHarness.Attach(
            new ScheduledTaskGrain(_db, _clock, NullLogger<IScheduledTaskGrain>.Instance),
            _fakes
        );

    private Task SubscribeAsync(
        ScheduledTaskGrain grain,
        string schedule,
        bool runMissed = false
    ) =>
        grain.SubscribeAsync(
            "server-1",
            _server,
            [new ScheduledTaskEntry(KEY, "rares", schedule, runMissed)],
            Ct
        );

    private ScheduledTaskResult Result(ScheduledTaskOutcome outcome, string? error = null) =>
        new(outcome, _clock.GetUtcNow(), 5, error, false);

    private sealed class RecordingServer : IScheduledTaskServer
    {
        public List<(
            string Key,
            Guid RunId,
            DateTimeOffset ScheduledFor,
            bool Manual
        )> Starts { get; } = [];

        public Task<bool> StartAsync(
            string key,
            Guid runId,
            DateTimeOffset scheduledFor,
            bool manual,
            CancellationToken ct
        )
        {
            Starts.Add((key, runId, scheduledFor, manual));

            return Task.FromResult(true);
        }
    }
}
