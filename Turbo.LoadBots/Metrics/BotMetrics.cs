using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Turbo.LoadBots.Metrics;

/// <summary>How sure a failed check is to be a server fault.</summary>
public enum CheckSeverity
{
    /// <summary>The server broke its own contract: a reply missing, malformed or wrong.</summary>
    Hard,

    /// <summary>
    /// Could be the server, could be the bot's world model (a path another avatar blocked, a
    /// tile a furni landed on a moment earlier). Watch the rate, not the single failure.
    /// </summary>
    Soft,
}

/// <summary>
/// What every bot records, shared by all of them: how long each request took to answer, how
/// each correctness check went, and plain counters (packets, bytes, disconnects).
/// </summary>
public sealed class BotMetrics
{
    private const int MAX_SAMPLES_PER_OPERATION = 50_000;
    private const int MAX_FAILURE_EXAMPLES = 25;

    private readonly ConcurrentDictionary<string, OperationStats> _operations = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<string, CheckStats> _checks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.Ordinal);

    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    /// <summary>One answered (or unanswered) request.</summary>
    public void Operation(string name, TimeSpan elapsed, bool succeeded) =>
        _operations.GetOrAdd(name, _ => new OperationStats()).Add(elapsed, succeeded);

    /// <summary>One correctness check. Returns <paramref name="passed"/> for chaining.</summary>
    public bool Check(
        string name,
        bool passed,
        CheckSeverity severity = CheckSeverity.Hard,
        string? detail = null
    )
    {
        _checks.GetOrAdd(name, _ => new CheckStats(severity)).Add(passed, detail);

        return passed;
    }

    public void Count(string name, long delta = 1) =>
        _counters.AddOrUpdate(name, delta, (_, value) => value + delta);

    public long Counter(string name) => _counters.TryGetValue(name, out var value) ? value : 0;

    public bool AnyHardFailure =>
        _checks.Values.Any(x => x.Severity is CheckSeverity.Hard && x.Failed > 0);

    public MetricsReport Report()
    {
        var now = DateTime.UtcNow;

        return new MetricsReport
        {
            StartedAtUtc = StartedAtUtc,
            Seconds = (now - StartedAtUtc).TotalSeconds,
            Operations = _operations
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value.Summary(), StringComparer.Ordinal),
            Checks = _checks
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value.Summary(), StringComparer.Ordinal),
            Counters = _counters
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
        };
    }

    private sealed class OperationStats
    {
        private readonly Lock _lock = new();
        private readonly List<double> _samples = [];
        private long _count;
        private long _failed;
        private long _seen;
        private double _max;
        private double _sum;

        public void Add(TimeSpan elapsed, bool succeeded)
        {
            var ms = elapsed.TotalMilliseconds;

            lock (_lock)
            {
                _count++;

                if (!succeeded)
                {
                    _failed++;

                    return;
                }

                _sum += ms;
                _max = Math.Max(_max, ms);
                _seen++;

                // Reservoir sampling keeps the percentiles honest over a long run in bounded memory.
                if (_samples.Count < MAX_SAMPLES_PER_OPERATION)
                    _samples.Add(ms);
                else
                {
                    var slot = Random.Shared.NextInt64(_seen);

                    if (slot < MAX_SAMPLES_PER_OPERATION)
                        _samples[(int)slot] = ms;
                }
            }
        }

        public OperationSummary Summary()
        {
            lock (_lock)
            {
                var sorted = _samples.Order().ToArray();
                var succeeded = _count - _failed;

                return new OperationSummary
                {
                    Count = _count,
                    Failed = _failed,
                    MeanMs = succeeded == 0 ? 0 : Math.Round(_sum / succeeded, 2),
                    P50Ms = Percentile(sorted, 0.50),
                    P95Ms = Percentile(sorted, 0.95),
                    P99Ms = Percentile(sorted, 0.99),
                    MaxMs = Math.Round(_max, 2),
                };
            }
        }

        private static double Percentile(double[] sorted, double p) =>
            sorted.Length == 0
                ? 0
                : Math.Round(sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))], 2);
    }

    private sealed class CheckStats(CheckSeverity severity)
    {
        private readonly Lock _lock = new();
        private readonly List<string> _examples = [];
        private long _passed;
        private long _failed;

        public CheckSeverity Severity { get; } = severity;

        public long Failed => Interlocked.Read(ref _failed);

        public void Add(bool passed, string? detail)
        {
            lock (_lock)
            {
                if (passed)
                {
                    _passed++;

                    return;
                }

                _failed++;

                if (detail is not null && _examples.Count < MAX_FAILURE_EXAMPLES)
                    _examples.Add(detail);
            }
        }

        public CheckSummary Summary()
        {
            lock (_lock)
            {
                return new CheckSummary
                {
                    Severity = Severity.ToString(),
                    Passed = _passed,
                    Failed = _failed,
                    Examples = [.. _examples],
                };
            }
        }
    }
}

public sealed record MetricsReport
{
    public required DateTime StartedAtUtc { get; init; }
    public required double Seconds { get; init; }
    public required Dictionary<string, OperationSummary> Operations { get; init; }
    public required Dictionary<string, CheckSummary> Checks { get; init; }
    public required Dictionary<string, long> Counters { get; init; }
}

public sealed record OperationSummary
{
    public required long Count { get; init; }
    public required long Failed { get; init; }
    public required double MeanMs { get; init; }
    public required double P50Ms { get; init; }
    public required double P95Ms { get; init; }
    public required double P99Ms { get; init; }
    public required double MaxMs { get; init; }
}

public sealed record CheckSummary
{
    public required string Severity { get; init; }
    public required long Passed { get; init; }
    public required long Failed { get; init; }
    public required IReadOnlyList<string> Examples { get; init; }
}
