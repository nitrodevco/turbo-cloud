using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

/// <summary>The latency test's report, after latencytest.report.index pings (LatencyTracker.as).</summary>
public record LatencyPingReportMessage : IClientTimerMessage
{
    public int AverageLatency { get; init; }
    public int ValidPingAverage { get; init; }
    public int NumPings { get; init; }
}
