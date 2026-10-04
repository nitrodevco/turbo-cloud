using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.LoadBots.Behaviour;
using Turbo.LoadBots.Behaviour.Activities;
using Turbo.LoadBots.Metrics;

namespace Turbo.LoadBots.Runner;

/// <summary>Everything a run found, as written to the report file.</summary>
public sealed record RunReport
{
    public required string Mode { get; init; }
    public required int Bots { get; init; }
    public required Dictionary<string, int> Personas { get; init; }
    public required MetricsReport Metrics { get; init; }
    public required IReadOnlyDictionary<string, string> WiredOutcomes { get; init; }
    public required IReadOnlyList<string> WiredNotTried { get; init; }
}

/// <summary>
/// One progress line at a time: who is online, packet throughput since the last line, the
/// failures so far and the slowest operations.
/// </summary>
public sealed class ProgressMeter(BotMetrics metrics)
{
    private long _lastPackets;
    private DateTime _lastAt = DateTime.UtcNow;

    public string Line(int online, int total)
    {
        var now = DateTime.UtcNow;
        var packets = metrics.Counter("packets.received") + metrics.Counter("packets.sent");
        var rate = (packets - _lastPackets) / Math.Max(0.001, (now - _lastAt).TotalSeconds);

        _lastPackets = packets;
        _lastAt = now;

        var report = metrics.Report();
        var hard = report
            .Checks.Values.Where(x => x.Severity == nameof(CheckSeverity.Hard))
            .Sum(x => x.Failed);
        var soft = report
            .Checks.Values.Where(x => x.Severity == nameof(CheckSeverity.Soft))
            .Sum(x => x.Failed);
        var slowest = report
            .Operations.OrderByDescending(x => x.Value.P95Ms)
            .Take(3)
            .Select(x =>
                string.Create(CultureInfo.InvariantCulture, $"{x.Key} p95 {x.Value.P95Ms:0}ms")
            );

        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{report.Seconds, 6:0}s] online {online}/{total} | {rate:0} pkt/s | hard failures {hard} | soft failures {soft} | {string.Join(", ", slowest)}"
        );
    }
}

/// <summary>The console summary and the report file at the end of a run.</summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions JSON = new() { WriteIndented = true };

    /// <summary>The end-of-run tables: every operation's latency and every check that failed.</summary>
    public static string Summary(RunReport report)
    {
        var text = new StringBuilder();
        var metrics = report.Metrics;

        text.AppendLine(
            CultureInfo.InvariantCulture,
            $"=== {report.Mode}: {report.Bots} bots, {metrics.Seconds:0}s ==="
        );
        text.AppendLine(
            "operation                                  count  failed    mean     p50     p95     p99     max"
        );

        foreach (var (name, op) in metrics.Operations)
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"{name, -40} {op.Count, 7} {op.Failed, 7} {op.MeanMs, 7:0.0} {op.P50Ms, 7:0.0} {op.P95Ms, 7:0.0} {op.P99Ms, 7:0.0} {op.MaxMs, 7:0.0}"
            );

        var failing = metrics.Checks.Where(x => x.Value.Failed > 0).ToList();

        text.AppendLine();
        text.AppendLine(
            CultureInfo.InvariantCulture,
            $"checks: {metrics.Checks.Count} kinds, {metrics.Checks.Values.Sum(x => x.Passed)} passed, {metrics.Checks.Values.Sum(x => x.Failed)} failed"
        );

        foreach (
            var (name, check) in failing.OrderBy(x => x.Value.Severity, StringComparer.Ordinal)
        )
        {
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"  [{check.Severity}] {name}: {check.Failed} failed / {check.Passed} passed"
            );

            foreach (var example in check.Examples.Take(5))
                text.AppendLine(CultureInfo.InvariantCulture, $"      - {example}");
        }

        if (report.WiredOutcomes.Count > 0)
        {
            var rejected = report
                .WiredOutcomes.Where(x =>
                    x.Value
                        is not (
                            "saved"
                            or "refused (staff only)"
                            or WiredActivities.KNOWN_GAP_OUTCOME
                        )
                )
                .ToList();

            text.AppendLine();
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"wired: {report.WiredOutcomes.Count} box types tried, {report.WiredOutcomes.Count(x => x.Value == "saved")} saved, {report.WiredNotTried.Count} not reached"
            );

            foreach (var (logic, outcome) in rejected)
                text.AppendLine(CultureInfo.InvariantCulture, $"  {logic}: {outcome}");

            var gaps = report
                .WiredOutcomes.Where(x => x.Value == WiredActivities.KNOWN_GAP_OUTCOME)
                .Select(x => x.Key)
                .ToList();

            if (gaps.Count > 0)
                text.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  known gaps, no server logic yet ({gaps.Count}): {string.Join(", ", gaps)}"
                );
        }

        return text.ToString();
    }

    public static async Task<string> WriteAsync(
        string pattern,
        RunReport report,
        CancellationToken ct
    )
    {
        var path = pattern.Replace(
            "{time}",
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            StringComparison.Ordinal
        );

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, report, JSON, ct);

        return Path.GetFullPath(path);
    }

    public static Dictionary<string, int> CountPersonas(IEnumerable<BotAgent> agents) =>
        agents
            .GroupBy(x => x.Context.Persona.ToString())
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
}
