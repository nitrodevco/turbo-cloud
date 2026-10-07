using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Admin.Performance;

/// <summary>
/// The Performance page's figures, taken in this server's own process: every
/// <c>PerformanceSampleSeconds</c> the CPU, memory, garbage collection, thread pool, players online
/// and rooms loaded, and between samples every room and command timing the hotel measures
/// (<see cref="RoomTelemetry"/>, <see cref="CommandTelemetry"/>), heard by listening to their
/// meters. Kept in memory for <c>PerformanceHistoryHours</c>; nothing is written anywhere. Only
/// runs while the admin API is on, since listening is what turns the timings on.
/// </summary>
public sealed class AdminPerformanceRecorder(
    IOptions<AdminConfig> config,
    ISessionGateway sessions,
    IGrainFactory grainFactory,
    TimeProvider timeProvider,
    ILogger<AdminPerformanceRecorder> logger
) : BackgroundService
{
    /// <summary>The stage the room stream's delivery delay is kept under; it has no stage of its own.</summary>
    public const string STREAM_DELIVERY = "room.stream.delivery";

    /// <summary>The timings kept per stage per sample; past this, a fair sample of them is kept.</summary>
    private const int RESERVOIR = 256;

    private readonly Lock _gate = new();
    private readonly LinkedList<Sample> _samples = new();
    private Dictionary<string, StageWindow> _window = [];
    private MeterListener? _listener;
    private DateTime? _since;
    private Baseline? _baseline;

    /// <summary>Starts hearing the hotel's timings. Called once the API is on; safe to call again.</summary>
    public void StartListening()
    {
        lock (_gate)
        {
            if (_listener is not null)
                return;

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (
                        instrument.Name
                        is RoomTelemetry.DURATION_NAME
                            or RoomTelemetry.STREAM_DELAY_NAME
                            or CommandTelemetry.DURATION_NAME
                    )
                        listener.EnableMeasurementEvents(instrument);
                },
            };
            _listener.SetMeasurementEventCallback<double>(OnMeasurement);
            _listener.Start();
        }
    }

    /// <summary>
    /// Takes one sample: the figures since the last one, and the timings heard since. The first
    /// call only sets where the CPU and collection counts are counted from.
    /// </summary>
    public async Task SampleAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var current = Baseline.Now(now);
        var players = sessions.GetOnlinePlayerIds().Count;
        var rooms = await RoomsLoadedAsync(ct).ConfigureAwait(false);

        lock (_gate)
        {
            var previous = _baseline;

            _baseline = current;
            _since ??= now;

            var stages = _window.ToDictionary(x => x.Key, x => x.Value.Snapshot());

            _window = [];

            if (previous is null)
                return;

            var wall = (current.At - previous.At).TotalMilliseconds;

            if (wall <= 0)
                return;

            _samples.AddLast(
                new Sample(
                    now,
                    Math.Clamp(
                        (current.Cpu - previous.Cpu).TotalMilliseconds
                            / (wall * Environment.ProcessorCount)
                            * 100,
                        0,
                        100
                    ),
                    current.WorkingSetMb,
                    current.HeapMb,
                    current.Gen2 - previous.Gen2,
                    Math.Clamp(
                        (current.GcPause - previous.GcPause).TotalMilliseconds / wall * 100,
                        0,
                        100
                    ),
                    current.ThreadPoolQueue,
                    current.ThreadPoolThreads,
                    players,
                    rooms,
                    stages
                )
            );

            var oldest =
                now - TimeSpan.FromHours(Math.Max(1, config.Value.PerformanceHistoryHours));

            while (_samples.First is { } first && first.Value.AtUtc < oldest)
                _samples.RemoveFirst();
        }
    }

    /// <summary>
    /// The last <paramref name="hours"/> of samples, merged into at most
    /// <c>PerformanceMaxPoints</c> points, and each stage's timings over all of them.
    /// </summary>
    public PerformanceResponse Read(int hours)
    {
        var options = config.Value;

        hours = Math.Clamp(hours, 1, Math.Max(1, options.PerformanceHistoryHours));

        List<Sample> samples;
        DateTime? since;

        lock (_gate)
        {
            var from = timeProvider.GetUtcNow().UtcDateTime - TimeSpan.FromHours(hours);

            samples = [.. _samples.Where(x => x.AtUtc >= from)];
            since = _since;
        }

        var perPoint = Math.Max(
            1,
            (int)Math.Ceiling(samples.Count / (double)Math.Max(1, options.PerformanceMaxPoints))
        );
        var sampleSeconds = Math.Max(1, options.PerformanceSampleSeconds);

        return new PerformanceResponse(
            hours,
            sampleSeconds,
            sampleSeconds * perPoint,
            since,
            [.. samples.Chunk(perPoint).Select(Point)],
            [
                .. samples
                    .SelectMany(x => x.Stages)
                    .GroupBy(x => x.Key)
                    .Select(x => Stage(x.Key, [.. x.Select(y => y.Value)]))
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Stage, StringComparer.Ordinal),
            ]
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Value.Enabled)
            return;

        StartListening();

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(1, config.Value.PerformanceSampleSeconds)),
            timeProvider
        );

        try
        {
            do
            {
                try
                {
                    await SampleAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not take a performance sample");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override void Dispose()
    {
        lock (_gate)
        {
            _listener?.Dispose();
            _listener = null;
        }

        base.Dispose();
    }

    private void OnMeasurement(
        Instrument instrument,
        double seconds,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state
    )
    {
        var stage = instrument.Name switch
        {
            RoomTelemetry.STREAM_DELAY_NAME => STREAM_DELIVERY,
            CommandTelemetry.DURATION_NAME => CommandTelemetry.EXECUTE,
            _ => StageOf(tags),
        };

        if (stage is null)
            return;

        lock (_gate)
        {
            if (!_window.TryGetValue(stage, out var window))
                _window[stage] = window = new StageWindow();

            window.Add(seconds * 1000);
        }
    }

    private static string? StageOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
            if (tag.Key == "stage")
                return tag.Value as string;

        return null;
    }

    private async Task<int> RoomsLoadedAsync(CancellationToken ct)
    {
        try
        {
            return (
                await grainFactory
                    .GetRoomDirectoryGrain()
                    .GetActiveRoomIdsAsync(ct)
                    .ConfigureAwait(false)
            ).Length;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not count the loaded rooms for a performance sample");

            return 0;
        }
    }

    /// <summary>Samples merged into one point: averages for levels, sums for counts, timings pooled.</summary>
    private static PerformancePoint Point(Sample[] samples)
    {
        var entries = Pooled(samples, RoomTelemetry.DIRECT_ENTRY);
        var delivery = Pooled(samples, STREAM_DELIVERY);

        return new PerformancePoint(
            samples[^1].AtUtc,
            Math.Round(samples.Average(x => x.CpuPercent), 1),
            (long)samples.Average(x => x.WorkingSetMb),
            (long)samples.Average(x => x.HeapMb),
            samples.Sum(x => x.Gen2Collections),
            Math.Round(samples.Average(x => x.GcPausePercent), 2),
            (long)Math.Round(samples.Average(x => x.ThreadPoolQueue)),
            (int)Math.Round(samples.Average(x => x.ThreadPoolThreads)),
            (int)Math.Round(samples.Average(x => x.PlayersOnline)),
            (int)Math.Round(samples.Average(x => x.RoomsLoaded)),
            entries.Count,
            Percentile(entries.Values, 0.5),
            Percentile(entries.Values, 0.95),
            Percentile(delivery.Values, 0.95)
        );
    }

    private static (int Count, double[] Values) Pooled(Sample[] samples, string stage)
    {
        var windows = samples
            .Select(x => x.Stages.GetValueOrDefault(stage))
            .OfType<StageSnapshot>()
            .ToList();

        return (windows.Sum(x => x.Count), [.. windows.SelectMany(x => x.ValuesMs)]);
    }

    private static PerformanceStage Stage(string stage, List<StageSnapshot> windows)
    {
        double[] values = [.. windows.SelectMany(x => x.ValuesMs)];

        return new PerformanceStage(
            stage,
            windows.Sum(x => x.Count),
            Percentile(values, 0.5) ?? 0,
            Percentile(values, 0.95) ?? 0,
            Math.Round(windows.Max(x => x.MaxMs), 1)
        );
    }

    /// <summary>The nearest-rank percentile, in whole tenths of a millisecond; null for none.</summary>
    private static double? Percentile(double[] values, double p)
    {
        if (values.Length == 0)
            return null;

        var sorted = values.Order().ToArray();
        var rank = Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1);

        return Math.Round(sorted[rank], 1);
    }

    /// <summary>The process's running totals at one moment, which samples are the differences of.</summary>
    private sealed record Baseline(
        DateTime At,
        TimeSpan Cpu,
        int Gen2,
        TimeSpan GcPause,
        long WorkingSetMb,
        long HeapMb,
        long ThreadPoolQueue,
        int ThreadPoolThreads
    )
    {
        public static Baseline Now(DateTime at)
        {
            using var process = Process.GetCurrentProcess();

            return new Baseline(
                at,
                process.TotalProcessorTime,
                GC.CollectionCount(2),
                GC.GetTotalPauseDuration(),
                process.WorkingSet64 / 1024 / 1024,
                GC.GetTotalMemory(false) / 1024 / 1024,
                ThreadPool.PendingWorkItemCount,
                ThreadPool.ThreadCount
            );
        }
    }

    private sealed record Sample(
        DateTime AtUtc,
        double CpuPercent,
        long WorkingSetMb,
        long HeapMb,
        int Gen2Collections,
        double GcPausePercent,
        long ThreadPoolQueue,
        int ThreadPoolThreads,
        int PlayersOnline,
        int RoomsLoaded,
        Dictionary<string, StageSnapshot> Stages
    );

    private sealed record StageSnapshot(int Count, double MaxMs, double[] ValuesMs);

    /// <summary>
    /// One stage's timings in the sample being taken: all of them counted, the longest kept, and
    /// up to <see cref="RESERVOIR"/> kept for the percentiles, each timing equally likely to be.
    /// </summary>
    private sealed class StageWindow
    {
        private readonly List<double> _values = [];
        private int _count;
        private double _max;

        public void Add(double ms)
        {
            _count++;
            _max = Math.Max(_max, ms);

            if (_values.Count < RESERVOIR)
                _values.Add(ms);
            else if (Random.Shared.Next(_count) is var slot && slot < RESERVOIR)
                _values[slot] = ms;
        }

        public StageSnapshot Snapshot() => new(_count, _max, [.. _values]);
    }
}
