using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Admin.Performance;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The Performance page's figures: the hotel's own room timings heard as they are measured, the
/// players and rooms of each sample, a day kept and no more, and long ranges merged into few points.
/// </summary>
public sealed class AdminPerformanceRecorderTests : IDisposable
{
    private static readonly DateTime START = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(START));
    private readonly List<AdminPerformanceRecorder> _recorders = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminPerformanceRecorderTests()
    {
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)[new PlayerId(1), new PlayerId(2), new PlayerId(3)];
        _fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create(new RoomId(10), new RoomId(11)));
    }

    public void Dispose()
    {
        foreach (var recorder in _recorders)
            recorder.Dispose();

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TheHotelsRoomTimings_AreHeard_AndReportedPerStage()
    {
        // A stage of this test's own: other tests measure rooms at the same time.
        var stage = $"test.stage.{Guid.NewGuid():N}";
        var recorder = Recorder();

        recorder.StartListening();
        await recorder.SampleAsync(Ct);

        await RoomTelemetry.MeasureAsync(stage, new RoomId(10), () => Task.Delay(30, Ct));
        await RoomTelemetry.MeasureAsync(stage, new RoomId(10), () => Task.CompletedTask);
        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.DIRECT_ENTRY,
            new RoomId(10),
            () => Task.Delay(20, Ct)
        );

        _time.Advance(TimeSpan.FromSeconds(10));
        await recorder.SampleAsync(Ct);

        var read = recorder.Read(1);
        var timing = read.Stages.Single(x => x.Stage == stage);

        timing.Count.Should().Be(2);
        timing.MaxMs.Should().BeGreaterThanOrEqualTo(25);
        timing.P95Ms.Should().Be(timing.MaxMs);

        var point = read.Points.Should().ContainSingle().Subject;

        point.RoomEntries.Should().BeGreaterThanOrEqualTo(1);
        point.RoomEntryP95Ms.Should().BeGreaterThanOrEqualTo(15);
        point.PlayersOnline.Should().Be(3);
        point.RoomsLoaded.Should().Be(2);
        point.AtUtc.Should().Be(START.AddSeconds(10));
    }

    [Fact]
    public async Task TheFirstSample_OnlySetsWhereTheCountsStartFrom()
    {
        var recorder = Recorder();

        await recorder.SampleAsync(Ct);

        var read = recorder.Read(1);

        read.Points.Should().BeEmpty();
        read.RecordingSinceUtc.Should().Be(START);
    }

    [Fact]
    public async Task ALongRange_IsMergedIntoTheMostPointsAChartIsSent()
    {
        var recorder = Recorder(maxPoints: 2);

        for (var i = 0; i <= 4; i++)
        {
            await recorder.SampleAsync(Ct);
            _time.Advance(TimeSpan.FromSeconds(10));
        }

        var read = recorder.Read(1);

        read.Points.Should().HaveCount(2, "four samples, at most two points");
        read.PointSeconds.Should().Be(20);
        read.Points[^1].AtUtc.Should().Be(START.AddSeconds(40));
    }

    [Fact]
    public async Task OnlyTheHistoryHoursAreKept()
    {
        var recorder = Recorder(historyHours: 1);

        await recorder.SampleAsync(Ct);
        _time.Advance(TimeSpan.FromSeconds(10));
        await recorder.SampleAsync(Ct);
        _time.Advance(TimeSpan.FromHours(2));
        await recorder.SampleAsync(Ct);

        // Asking for more than is kept is asking for all of it.
        var read = recorder.Read(24);

        read.Hours.Should().Be(1);
        read.Points.Should().ContainSingle().Which.AtUtc.Should().Be(_time.GetUtcNow().UtcDateTime);
    }

    private AdminPerformanceRecorder Recorder(int maxPoints = 360, int historyHours = 24)
    {
        var recorder = new AdminPerformanceRecorder(
            Options.Create(
                new AdminConfig
                {
                    PerformanceSampleSeconds = 10,
                    PerformanceMaxPoints = maxPoints,
                    PerformanceHistoryHours = historyHours,
                }
            ),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<IGrainFactory>(),
            _time,
            NullLogger<AdminPerformanceRecorder>.Instance
        );

        _recorders.Add(recorder);

        return recorder;
    }
}
